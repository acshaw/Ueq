# Deploying the Ueq content stack to AWS (5.11)

Architecture (see `docs/devplans/5.11-web-api-cicd-aws-hosting.md` for the full reasoning):

```
Browser (anyone — access gated by login, not by IP)
  └── Caddy (auto HTTPS via sslip.io) on one Lightsail instance
        ├── /            -> static Angular build  (/var/www/ueq-web), shows a login screen until
        │                    the session cookie (JWT) checks out
        ├── /api/*        -> local Kestrel process (ueq-api.service, :5144) — every endpoint
        │                    requires a valid session except /api/auth/*
        └── Postgres      -> Docker, 127.0.0.1 only, never exposed
```

Deliberately **not** `family-cookbook`'s S3+CloudFront+Lambda+DSQL shape — see the devplan's Grounding
section for why (different DB compatibility needs: plain Postgres, not DSQL, to match Ueq's existing
`BIGSERIAL`-based schema and the 2.11 export/import tool).

**Access control note (revised 2026-07-19):** the original plan IP-allowlisted the firewall to the user's
and their brother's home IPs. Both turned out to be dynamic, making that a recurring manual chore — so
this was replaced with real application-level auth instead (a `web_admins` table, JWT session cookies,
registration gated by a shared invite code). The firewall is now open to everyone on 80/443; the login
screen is the actual gate. See CD5 in the devplan for the full reasoning.

GitHub Actions deploys via **OIDC** (reuses this AWS account's existing GitHub identity provider
registration from `family-cookbook` — no need to re-register it) + **AWS Systems Manager** (no SSH key
stored anywhere, no SSH port open to the internet at all).

---

## 0. Prerequisites

- AWS CLI v2, already configured on this machine (confirmed working this session).
- Decide on values and export them once, so every command below is copy-pasteable:

```bash
export AWS_REGION=us-east-2
export ACCOUNT_ID=$(aws sts get-caller-identity --query Account --output text)
export INSTANCE_NAME=ueq-server
export DEPLOY_BUCKET=ueq-deploy-artifacts-$ACCOUNT_ID   # globally-unique S3 bucket name
export GH_REPO=<your-github-user>/<your-ueq-repo>        # e.g. acshaw/Ueq
```

---

## 1. Create the Lightsail instance

Look up the current bundle/blueprint ids live rather than trust hardcoded ones (these occasionally change
name across Lightsail generations):

```bash
# Find the ~1 GB RAM / $10-tier bundle (CD1) — confirm the id before using it below.
aws lightsail get-bundles --query "bundles[?ramSizeInGb==\`1\`].{id:bundleId,price:price}" --output table

# Find a current Ubuntu LTS blueprint.
aws lightsail get-blueprints --query "blueprints[?platform=='LINUX_UNIX' && contains(blueprintId,'ubuntu')].{id:blueprintId,name:name}" --output table
```

Then, using the ids from those two lists (as of this session, `micro_3_0` and `ubuntu_22_04` are the
expected values — confirm, don't assume):

```bash
export BUNDLE_ID=micro_3_0
export BLUEPRINT_ID=ubuntu_22_04

aws lightsail create-instances \
  --instance-names $INSTANCE_NAME \
  --availability-zone ${AWS_REGION}a \
  --blueprint-id $BLUEPRINT_ID \
  --bundle-id $BUNDLE_ID \
  --region $AWS_REGION
```

## 2. Attach a static IP

Without this, the public IP can change on a stop/start, which would break both the sslip.io hostname
(step 4) and the firewall CIDR rules (step 3). This is free while attached to a running instance.

```bash
aws lightsail allocate-static-ip --static-ip-name ueq-server-ip --region $AWS_REGION
aws lightsail attach-static-ip --static-ip-name ueq-server-ip --instance-name $INSTANCE_NAME --region $AWS_REGION

export STATIC_IP=$(aws lightsail get-static-ip --static-ip-name ueq-server-ip --region $AWS_REGION --query staticIp.ipAddress --output text)
echo $STATIC_IP
```

## 3. Configure the firewall — open 80/443 to everyone, SSH closed entirely (CD5, revised)

**This command replaces the entire rule set** — anything not listed here gets closed, including the
default SSH+80 rules Lightsail opens on instance creation. No port 22 rule means SSH is closed to the
internet, full stop — manual admin access, when needed, goes through Lightsail's built-in browser-based SSH
(console-authenticated, not network-exposed). 80/443 are open to `0.0.0.0/0` — access is gated by the
app's own login (a JWT session cookie, registration behind a shared invite code), not by network location,
since both home IPs turned out to be dynamic and an IP allowlist would've meant re-running this command
every time an ISP rotated one.

```bash
aws lightsail put-instance-public-ports \
  --instance-name $INSTANCE_NAME \
  --region $AWS_REGION \
  --port-infos "[
    {\"fromPort\":80,\"toPort\":80,\"protocol\":\"tcp\",\"cidrs\":[\"0.0.0.0/0\"]},
    {\"fromPort\":443,\"toPort\":443,\"protocol\":\"tcp\",\"cidrs\":[\"0.0.0.0/0\"]},
    {\"fromPort\":22,\"toPort\":22,\"protocol\":\"tcp\",\"cidrListAliases\":[\"lightsail-connect\"]}
  ]"
```

**Gotcha (found 2026-07-19):** the browser-based SSH client Lightsail's console offers isn't actually
network-exposed SSH — it connects through a special firewall alias (`lightsail-connect`) that Lightsail
provisions by default alongside the instance's default SSH rule. Omitting a port-22 rule entirely (as an
earlier version of this doc did) also kills the browser SSH client, not just network SSH — the console
shows "The SSH client is not available" if you do. The rule above keeps SSH closed to the real internet
(`cidrListAliases` scopes it to AWS's own browser-SSH proxy, not `0.0.0.0/0`) while keeping the console
client usable.

On Windows, `aws.cmd` reprocesses arguments through `cmd.exe`, which strips embedded double quotes even
from inside a PowerShell single-quoted string — the multi-line `\"..\"`-escaped form above works from a
real bash/zsh shell, but from PowerShell, write the JSON to a file and pass `--port-infos file://path.json`
instead.

## 4. DNS via sslip.io (CD4)

No domain purchase, no DNS record to create — `sslip.io` resolves `<ip-with-dashes>.sslip.io` to that IP
automatically. Convert the static IP's dots to dashes:

```bash
export UEQ_HOSTNAME="$(echo $STATIC_IP | tr '.' '-').sslip.io"
echo $UEQ_HOSTNAME
# sanity check it resolves to the right place from your own machine (not this sandbox):
#   nslookup $UEQ_HOSTNAME
```

## 5. Connect to the instance and install Docker + Postgres

Use Lightsail's browser-based SSH (console → your instance → **Connect using SSH**) for this one-time
setup — no local SSH client or key needed.

```bash
# On the instance:
curl -fsSL https://get.docker.com | sudo sh
sudo usermod -aG docker $(whoami)
# log out/back in (or `newgrp docker`) for the group change to take effect

mkdir -p ~/ueq-db && cd ~/ueq-db
```

Copy this project's existing `docker-compose.yml` (same one you run locally — via `scp` from the browser
console isn't available, so paste its contents directly, or use Lightsail's SSH file-manager feature if
your browser session supports it). Then:

```bash
docker compose up -d
docker exec ueq_postgres pg_isready -U ueq   # confirm it's accepting connections
```

Postgres here is **never exposed** — `docker-compose.yml`'s port mapping should bind to `127.0.0.1:5432`
only (double-check this against the version you copy over; the local dev version may bind `0.0.0.0` since
localhost-only didn't matter on your own machine).

## 6. Install Caddy

```bash
sudo apt install -y debian-keyring debian-archive-keyring apt-transport-https curl
curl -1sLf 'https://dl.cloudsmith.io/public/caddy/stable/gpg.key' | sudo gpg --dearmor -o /usr/share/keyrings/caddy-stable-archive-keyring.gpg
curl -1sLf 'https://dl.cloudsmith.io/public/caddy/stable/debian.deb.txt' | sudo tee /etc/apt/sources.list.d/caddy-stable.list
sudo apt update
sudo apt install -y caddy
```

Copy `deploy/Caddyfile` from this repo to `/etc/caddy/Caddyfile` on the instance, then set the hostname env
var Caddy reads (`{$UEQ_HOSTNAME}` in the Caddyfile):

```bash
echo "UEQ_HOSTNAME=$UEQ_HOSTNAME" | sudo tee -a /etc/default/caddy
sudo mkdir -p /var/www/ueq-web   # empty for now — CD6's deploy step fills this in
```

**Gotcha (found 2026-07-19):** the Caddy `.deb` package's systemd unit does **not** load
`/etc/default/caddy` by default (that's a legacy init-script convention some packages wire up and this one
doesn't) — without the override below, `{$UEQ_HOSTNAME}` in the Caddyfile resolves to an empty string,
which makes Caddy treat the leading `{` as the **global options block** instead of a site block, and fail
to start with `Error: adapting config using caddyfile: ...: unrecognized global option: handle`. Fix with
an explicit systemd override before the first restart:

```bash
sudo systemctl edit caddy.service
# paste into the drop-in editor it opens:
#   [Service]
#   EnvironmentFile=/etc/default/caddy
sudo systemctl daemon-reload
sudo systemctl restart caddy
sudo systemctl status caddy   # confirm it's issuing a cert, not erroring
```

## 7. First-time API install

```bash
sudo apt install -y aspnetcore-runtime-10.0   # matches the api project's TargetFramework (net10.0)
sudo useradd --system --no-create-home ueq
sudo mkdir -p /opt/ueq/api
```

Create `/opt/ueq/api/api.env` (owned by root, mode 600 — never checked into git, never part of a deploy
artifact). `UEQ_WEB_JWT_SECRET` should be a long random string (e.g. `openssl rand -base64 48`, run
locally); `UEQ_WEB_INVITE_CODE` is whatever passphrase you and your brother will use to register — pick
something you can both remember and share once, out of band (not over an unencrypted channel):

```bash
sudo tee /opt/ueq/api/api.env > /dev/null <<'EOF'
UEQ_DB_CONNSTRING=Host=127.0.0.1;Port=5432;Database=ueq;Username=ueq;Password=<your-real-db-password>
UEQ_WEB_JWT_SECRET=<a-long-random-string>
UEQ_WEB_INVITE_CODE=<a-shared-passphrase-only-you-two-know>
EOF
sudo chmod 600 /opt/ueq/api/api.env
```

Both are required in Production — the API throws a clear startup error if either is missing (no silent
insecure fallback outside Development).

Copy `deploy/ueq-api.service` from this repo to `/etc/systemd/system/ueq-api.service`, then:

```bash
sudo systemctl daemon-reload
sudo systemctl enable ueq-api.service
# don't start yet — there's no published API binary in /opt/ueq/api/ until CD6's first deploy runs
```

## 8. One-time schema migration + content cutover (CD8 — manual, per the devplan)

From your **local machine**, temporarily point the Unity Editor at this instance's Postgres instead of
your local Docker Postgres:

1. Edit `db.config.json`: `host` → the instance's static IP, `port` → `5432`, `username`/`password` →
   match what you put in `api.env` above. (You'll need to temporarily open port 5432 to your own IP in
   the Lightsail firewall for this one step, or tunnel through the browser SSH session — port 5432 should
   go back to closed/localhost-only afterward.)

   **Gotcha (found 2026-07-20): the firewall rule alone isn't enough.** Step 5's `docker-compose.yml`
   binds Postgres to `127.0.0.1:5432:5432` (loopback-only) on purpose, so opening the Lightsail firewall
   to your IP doesn't actually make it reachable — Docker itself never listens on the box's public
   interface. Temporarily widen the binding too, on the instance (browser SSH):
   ```bash
   cd ~/ueq-db
   sed -i 's/127.0.0.1:5432:5432/5432:5432/' docker-compose.yml
   docker compose up -d
   docker ps   # confirm PORTS now shows 0.0.0.0:5432->5432/tcp, not 127.0.0.1:5432->5432/tcp
   ```
   Revert it back after step 8 finishes (see the end of this step) — don't leave Postgres open to the
   internet at large just because the firewall's IP restriction narrows who can reach it; the DB should
   go back to fully loopback-only, not just IP-restricted.
2. `Tools/Database/Run Migrations` — applies all pending migrations fresh (25 as of 2026-07-20; check
   `Assets/StreamingAssets/Database/Migrations/` for the current count, it grows over time).
3. `Tools/Database/Import Content...` → pick a fresh export from your real local dev DB (`Tools/Database/
   Export Content...` against local first) — this is the exact round-trip already verified this session for
   2.11, just pointed at the real target.
4. Revert `db.config.json` back to your local DB. Revert the Postgres port binding back to loopback-only
   and re-close the firewall:
   ```bash
   # on the instance, browser SSH:
   cd ~/ueq-db
   sed -i 's/5432:5432/127.0.0.1:5432:5432/' docker-compose.yml
   docker compose up -d
   docker ps   # confirm PORTS is back to 127.0.0.1:5432->5432/tcp
   ```
   ```powershell
   # locally — drop the 5432 rule, back to just 80/443/SSH-alias
   ```
   (re-run the step-3 firewall command with only the 80/443/22-`lightsail-connect` entries, dropping the
   5432 rule)

## 9. Enable AWS Systems Manager on the instance (CD6)

Lightsail instances aren't native EC2, so this uses Systems Manager's **hybrid activation** mechanism
(the same one used for on-premises/non-EC2 machines) rather than an EC2-style IAM instance profile.

```bash
cat > ssm-trust.json <<'EOF'
{ "Version":"2012-10-17","Statement":[{"Effect":"Allow",
  "Principal":{"Service":"ssm.amazonaws.com"},"Action":"sts:AssumeRole"}]}
EOF

aws iam create-role --role-name ueq-ssm-hybrid-role \
  --assume-role-policy-document file://ssm-trust.json

aws iam attach-role-policy --role-name ueq-ssm-hybrid-role \
  --policy-arn arn:aws:iam::aws:policy/AmazonSSMManagedInstanceCore

# Registration limit 1 (just this one instance); expiration gives you a window to run the
# register command on the box before the activation itself expires (not the same as the
# instance's registration, which persists).
aws ssm create-activation \
  --default-instance-name ueq-server \
  --iam-role ueq-ssm-hybrid-role \
  --registration-limit 1 \
  --region $AWS_REGION
```

Note the `ActivationCode` and `ActivationId` from the output — **they're shown once and aren't
retrievable again**. Then, on the instance (browser SSH):

```bash
sudo snap install amazon-ssm-agent --classic
sudo systemctl stop snap.amazon-ssm-agent.amazon-ssm-agent.service
sudo /snap/amazon-ssm-agent/current/amazon-ssm-agent -register \
  -code "<ActivationCode>" -id "<ActivationId>" -region "$AWS_REGION"
sudo systemctl start snap.amazon-ssm-agent.amazon-ssm-agent.service
```

Confirm it registered (may take a couple of minutes to appear):

```bash
aws ssm describe-instance-information --region $AWS_REGION \
  --query "InstanceInformationList[].{Id:InstanceId,PingStatus:PingStatus}" --output table
```

Note the resulting managed-instance id (looks like `mi-0123456789abcdef0`, distinct from an EC2 instance
id) — you'll need it for the deploy role's policy below.

```bash
export MANAGED_INSTANCE_ID=<mi-xxxxxxxxxxxxxxxxx>
```

## 10. Create the S3 deploy-artifacts bucket

```bash
aws s3 mb s3://$DEPLOY_BUCKET --region $AWS_REGION
```

Private by default (no public-access changes needed — only the OIDC deploy role and the instance's own
`aws s3 cp` pull need access).

## 11. GitHub OIDC deploy role (CD6)

The OIDC provider itself (`token.actions.githubusercontent.com`) is **already registered** on this account
from `family-cookbook` — skip re-registering it, just create a new role scoped to this repo:

```bash
cat > gh-trust.json <<EOF
{ "Version":"2012-10-17","Statement":[{"Effect":"Allow",
  "Principal":{"Federated":"arn:aws:iam::$ACCOUNT_ID:oidc-provider/token.actions.githubusercontent.com"},
  "Action":"sts:AssumeRoleWithWebIdentity",
  "Condition":{"StringEquals":{"token.actions.githubusercontent.com:aud":"sts.amazonaws.com"},
    "StringLike":{"token.actions.githubusercontent.com:sub":"repo:$GH_REPO:*"}}}]}
EOF

aws iam create-role --role-name ueq-gha-deploy \
  --assume-role-policy-document file://gh-trust.json

cat > gha-policy.json <<EOF
{ "Version":"2012-10-17","Statement":[
  {"Effect":"Allow","Action":["s3:PutObject","s3:GetObject"],
   "Resource":"arn:aws:s3:::$DEPLOY_BUCKET/*"},
  {"Effect":"Allow","Action":"ssm:SendCommand",
   "Resource":["arn:aws:ssm:$AWS_REGION::document/AWS-RunShellScript",
               "arn:aws:ssm:$AWS_REGION:$ACCOUNT_ID:managed-instance/$MANAGED_INSTANCE_ID"]},
  {"Effect":"Allow","Action":["ssm:GetCommandInvocation","ssm:ListCommandInvocations"],
   "Resource":"*"}]}
EOF

aws iam put-role-policy --role-name ueq-gha-deploy \
  --policy-name deploy --policy-document file://gha-policy.json
```

## 12. Wire up GitHub repo settings

**Secrets:**
| Name | Value |
|------|-------|
| `AWS_DEPLOY_ROLE_ARN` | `arn:aws:iam::<ACCOUNT_ID>:role/ueq-gha-deploy` |

**Variables:**
| Name | Value |
|------|-------|
| `AWS_REGION` | `us-east-2` |
| `DEPLOY_BUCKET` | your bucket name |
| `MANAGED_INSTANCE_ID` | `mi-...` from step 9 |

(`.github/workflows/deploy.yml` and `ci.yml` are added separately, once this one-time setup is confirmed
working — see the devplan's build order, step 4–5.)

## 13. Verify

Same case list already exercised locally this session (against a throwaway DB + the built API directly) —
repeat it here against the real deployed instance:

- `curl -i https://$UEQ_HOSTNAME/api/items` with no session → **401**.
- `curl -i -X POST https://$UEQ_HOSTNAME/api/auth/register` with a wrong invite code → **401**; with the
  real one → **200** + a `Set-Cookie` for `ueq_session`.
- Registering a second time with the same username → **409**.
- Visiting `https://$UEQ_HOSTNAME` in a browser shows the **login screen**, not the editor shell, until you
  actually sign in.
- `caddy`'s cert issuance succeeded (`sudo journalctl -u caddy | grep -i certificate`).

## 14. Deploy the dedicated game server (6.2)

See `docs/devplans/6.2-dedicated-server-build-hosting.md` (DH1–DH8) for the reasoning. This is a
fourth co-located process on the same box (CD9) — its own systemd unit, its own UDP firewall rule,
no Caddy involved (raw UDP, not HTTP).

### 14a. Open the firewall — add UDP 7777 to the existing rule set

`put-instance-public-ports` **replaces the whole rule set**, so re-list the 3 existing rules
alongside the new one:

```bash
aws lightsail put-instance-public-ports \
  --instance-name $INSTANCE_NAME \
  --region $AWS_REGION \
  --port-infos "[
    {\"fromPort\":80,\"toPort\":80,\"protocol\":\"tcp\",\"cidrs\":[\"0.0.0.0/0\"]},
    {\"fromPort\":443,\"toPort\":443,\"protocol\":\"tcp\",\"cidrs\":[\"0.0.0.0/0\"]},
    {\"fromPort\":22,\"toPort\":22,\"protocol\":\"tcp\",\"cidrListAliases\":[\"lightsail-connect\"]},
    {\"fromPort\":7777,\"toPort\":7777,\"protocol\":\"udp\",\"cidrs\":[\"0.0.0.0/0\"]}
  ]"
```

**DH6, restated:** this exposes an *unencrypted* KCP/UDP transport to the whole internet. Accepted
for now (6.3 hardens it) — same posture already applied to the open API.

### 14b. First-time instance setup (browser SSH)

```bash
sudo useradd --system --no-create-home ueq   # already exists if you did step 7 — harmless either way
sudo mkdir -p /opt/ueq/gameserver
```

Create `/opt/ueq/gameserver/gameserver.env` (root-owned, mode 600 — same convention as
`api.env`). `UEQ_DB_SEED=0` is deliberate: production content is authored via the web admin, not
`DatabaseSeeder`'s dev bootstrap data — the seeder's inserts are idempotent (`ON CONFLICT DO
NOTHING`) so leaving it on wouldn't corrupt anything, but there's no reason to run it against a
live DB either.

```bash
sudo tee /opt/ueq/gameserver/gameserver.env > /dev/null <<'EOF'
UEQ_DB_CONNSTRING=Host=127.0.0.1;Port=5432;Database=ueq;Username=ueq;Password=<your-real-db-password>
UEQ_DB_SEED=0
EOF
sudo chmod 600 /opt/ueq/gameserver/gameserver.env
```

Copy `deploy/ueq-gameserver.service` from this repo to `/etc/systemd/system/ueq-gameserver.service`,
then:

```bash
sudo systemctl daemon-reload
sudo systemctl enable ueq-gameserver.service
# don't start yet — there's no binary in /opt/ueq/gameserver/ until the first deploy below runs
```

**Note on migrations:** unlike the API, the Unity server calls `MigrationRunner.Run` on every
startup (established since 1.1) — so once this service is live, any pending migration gets applied
automatically the next time it restarts (including via a routine deploy), not just via the manual
`Tools/Database/Run Migrations` step in CD8. That's existing, already-reviewed server behavior
(idempotent, versioned `.sql` files you already committed), just worth knowing it now runs against
the real production DB too.

### 14c. Build the Linux Dedicated Server locally

In the Unity Editor: **`Tools/Build/Build Linux Dedicated Server`** — builds to
`C:\Builds\Ueq\ServerLinux` (binary `Ueq.x86_64` + `Ueq_Data/`). First run installs slower (fresh
platform switch); confirm `Result: Succeeded` in the Console.

### 14d. Upload the build to S3

The Linux server isn't built by CI (no Unity in GitHub Actions — DH7), so this step is manual, run
locally whenever the server code changes.

**Use `tar`, not a Windows zip tool.** PowerShell's `Compress-Archive` can write backslash path
separators inside the archive, which Linux's `unzip` can't parse for nested folders (`Ueq_Data/`
specifically — this bit us on the first real deploy). `tar` has no such ambiguity and ships
natively on both Windows 10+/Git Bash and Linux:

```bash
# from Git Bash, so `tar` is guaranteed present
cd "/c/Builds/Ueq/ServerLinux"
tar czf ../gameserver.tar.gz .   # tars the folder's *contents*, not the folder itself

aws s3 cp ../gameserver.tar.gz s3://$DEPLOY_BUCKET/gameserver.tar.gz
```

### 14e. Trigger the deploy

`deploy.yml` already presigns `gameserver.zip` and restarts `ueq-gameserver.service` on every run
(extended for 6.2) — either push to `main`, or run the **Deploy** workflow manually
(`workflow_dispatch`) from the GitHub Actions tab. Watch the "Show remote deploy output" step for
`== Deploying gameserver ==` / a successful `systemctl restart`.

### 14f. Verify

```bash
# on the instance, browser SSH:
sudo systemctl status ueq-gameserver.service
sudo journalctl -u ueq-gameserver -n 100
# expect the same [DB] Connected.../[Content] Loaded... lines 5.10 already validated the meaning of
```

Then, from your own machine: build a normal Standalone Windows client
(`Tools/Build/Build Standalone Client`), launch it, and on the Title screen's dev cluster set the
server address to `$UEQ_HOSTNAME` (or the static IP) instead of the `127.0.0.1` default — then run
the full register→login→spawn→target→combat→loot→camp→relog loop, same checklist 5.10's DS5 already
proved once locally, now proved over the real internet against the real AWS-hosted Postgres.

Simulate a crash to confirm supervision works: `sudo systemctl kill ueq-gameserver.service` →
`systemctl status` should show it auto-restarted within `RestartSec` (5s).

## 15. Client distribution + launcher (6.4)

See `docs/devplans/6.4-build-pipeline-launcher.md` for the reasoning. Gets a runnable, self-updating
client into the hands of someone who can't build it themselves (no Unity, no dev environment).

### 15a. One-time: point the game-server's version-mismatch message at the download page

Add to `/opt/ueq/gameserver/gameserver.env` (browser SSH):

```bash
echo 'UEQ_DOWNLOAD_URL=https://18-218-79-193.sslip.io/downloads/UeqLauncher.exe' | sudo tee -a /opt/ueq/gameserver/gameserver.env
sudo systemctl restart ueq-gameserver.service
```

### 15b. Cutting a release: stamp, build, zip, upload

In the Unity Editor: **`Tools/Build/Stamp New Build Id`** first (once per release — this is what
lets client and server detect a mismatch; skipping it means both keep whatever id was already
stamped), then build whichever of client/server actually changed
(`Tools/Build/Build Standalone Client`, `Tools/Build/Build Linux Dedicated Server`). **Always
rebuild and redeploy both together** if the scene/code changed at all — the established rule from
5.10/6.2 (a scene change alone desyncs Mirror's object hashing between an old server and a new
client) still applies; the build-id check now makes that class of mistake surface as a clear login
message instead of a confusing runtime symptom, but it doesn't make mismatched builds *work*.

```powershell
Compress-Archive -Path "C:\Builds\Ueq\Client\*" -DestinationPath "C:\Builds\Ueq\client.zip" -Force
aws s3 cp "C:\Builds\Ueq\client.zip" "s3://$env:DEPLOY_BUCKET/client.zip"
aws s3 cp "C:\Builds\Ueq\version.txt" "s3://$env:DEPLOY_BUCKET/version.txt"
```

(`Compress-Archive` is fine here, unlike the gameserver artifact — this zip is only ever extracted
by the launcher's own .NET code on a Windows machine, never by Linux `unzip`, so the
backslash-path-separator issue from 6.2 doesn't apply.)

Then upload the rebuilt Linux server per §14d/e if it changed, and push to trigger the deploy (it
presigns/redeploys client.zip + version.txt alongside web/api/gameserver automatically, same as
6.2 — see `deploy.yml`).

### 15c. One-time: build and hand off the launcher itself

The launcher's own logic is meant to stay simple/stable enough that it rarely needs rebuilding —
this is a one-time (or rare) step, not part of the routine release flow above.

```powershell
cd launcher\Ueq.Launcher
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
aws s3 cp "bin\Release\net10.0-windows\win-x64\publish\UeqLauncher.exe" "s3://$env:DEPLOY_BUCKET/UeqLauncher.exe"
```

`aws s3 cp` alone doesn't make the object downloadable through Caddy's `/downloads/*` path (that
serves `/var/www/ueq-downloads` on the instance, not the S3 bucket directly). `LAUNCHER_URL` is
wired into `deploy.sh`/`deploy.yml` the same way as `CLIENT_URL`/`VERSION_URL` — push to `main` (or
run the Deploy workflow manually) and it lands automatically. (An earlier version of this doc
suggested a manual browser-SSH `curl` of a presigned URL instead — don't do that: that console
corrupts long pasted lines with stray whitespace at display-wrap points, which breaks a presigned
URL's signature. The automated pipeline sidesteps the problem entirely.)

Send your brother the link to `https://18-218-79-193.sslip.io/downloads/UeqLauncher.exe` once. From
then on, running it always fetches whatever's newest — no more manual redownloads for him.

### 15d. Verify

- Build+stamp+deploy a release, run the launcher fresh (no prior local install) — downloads,
  extracts, launches, connects successfully.
- Rebuild only the server with a **new** stamp (don't rebuild/redeploy the client) and try
  connecting with the now-stale already-installed client directly (bypassing the launcher) —
  expect a clear "Your client is out of date" rejection, not a silent desync.
- Re-run the launcher after that mismatched test — it should detect the version change, redownload,
  and connect successfully again.

## Cost recap

| Item | Cost |
|---|---|
| Lightsail instance (micro tier) | $10/mo (possibly free for the first 3 months — first-time Lightsail promo, confirmed at instance-creation time in the console) |
| Static IP | Free while attached to a running instance |
| S3 (deploy artifacts, tiny) | Effectively $0 |
| SSM | Free (Send Command / hybrid activation, at this scale) |
| GitHub Actions | Free at this usage level |

## Known follow-ups (not this devplan)

- CD8: migrations to the deployed DB stay manual for now. A portable, non-Unity migration runner (so
  `deploy.yml` could auto-apply migrations) is an **expected fast-follow**, not just a someday-maybe — flagged
  during CD8's review.
- CD9: done as of 6.2 (§14) — the Unity dedicated server is a fourth process on this same box, its own
  systemd unit, its own UDP firewall rule. Whether it stays co-located or splits onto dedicated capacity is
  still a 6.5 (load testing) decision, not this one.
