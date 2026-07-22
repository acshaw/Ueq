#!/usr/bin/env bash
# Runs ON the Lightsail instance, invoked remotely by GitHub Actions via
# `aws ssm send-command` (CD6). Pulls the latest build artifacts from the private S3
# deploy bucket, unpacks them, and restarts the affected services.
#
# Expects DEPLOY_BUCKET to be set below (filled in during one-time setup — see
# docs/DEPLOY-AWS.md). Does NOT touch /opt/ueq/api/api.env (holds UEQ_DB_CONNSTRING) —
# that file is never part of the published API output, so a plain overwrite never
# clobbers it.
set -euo pipefail

DEPLOY_BUCKET="REPLACE_ME_ueq-deploy-artifacts-<account-id>"
WORKDIR="$(mktemp -d)"
trap 'rm -rf "$WORKDIR"' EXIT

echo "== Deploying web =="
aws s3 cp "s3://${DEPLOY_BUCKET}/web.zip" "${WORKDIR}/web.zip"
sudo rm -rf /var/www/ueq-web
sudo mkdir -p /var/www/ueq-web
sudo unzip -q -o "${WORKDIR}/web.zip" -d /var/www/ueq-web

echo "== Deploying api =="
aws s3 cp "s3://${DEPLOY_BUCKET}/api.zip" "${WORKDIR}/api.zip"
sudo unzip -q -o "${WORKDIR}/api.zip" -d /opt/ueq/api

echo "== Restarting services =="
sudo systemctl restart ueq-api.service
sudo systemctl reload caddy

echo "== Deploy complete =="
