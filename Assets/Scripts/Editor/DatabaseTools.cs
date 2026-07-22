using System;
using Npgsql;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor conveniences for the local Postgres dev DB (roadmap 1.1):
/// test the connection, apply migrations, and reset the schema ledger.
/// </summary>
public static class DatabaseTools
{
    [MenuItem("Tools/Database/Test Connection")]
    public static void TestConnection()
    {
        try
        {
            using var conn = Database.OpenEditorConnection();
            using var cmd = new NpgsqlCommand("SELECT version();", conn);
            var version = cmd.ExecuteScalar() as string;
            Debug.Log($"[DB] Connected to {conn.DataSource}/{conn.Database}\n{version}");
            EditorUtility.DisplayDialog("Database", $"Connected.\n\n{version}", "OK");
        }
        catch (Exception e)
        {
            Debug.LogError($"[DB] Test Connection failed: {e.Message}\n{e}");
            EditorUtility.DisplayDialog("Database", $"FAILED\n\n{e.Message}", "OK");
        }
    }

    [MenuItem("Tools/Database/Run Migrations")]
    public static void RunMigrations()
    {
        try
        {
            using var conn = Database.OpenEditorConnection();
            int n = MigrationRunner.Run(conn);
            var msg = n == 0 ? "No pending migrations." : $"Applied {n} migration(s).";
            Debug.Log($"[DB] {msg}");
            EditorUtility.DisplayDialog("Database", msg, "OK");
        }
        catch (Exception e)
        {
            Debug.LogError($"[DB] Run Migrations failed: {e.Message}\n{e}");
            EditorUtility.DisplayDialog("Database", $"FAILED\n\n{e.Message}", "OK");
        }
    }

    [MenuItem("Tools/Database/Seed Database")]
    public static void SeedDatabase()
    {
        try
        {
            using var conn = Database.OpenEditorConnection();
            DatabaseSeeder.Seed(conn);
            Debug.Log("[DB] Seed complete (idempotent — existing rows left intact).");
            EditorUtility.DisplayDialog("Database",
                "Seed complete.\n\nReference content (items, mobs, vendors, conversations, dev account) " +
                "inserted where missing. Existing rows were left intact.", "OK");
        }
        catch (Exception e)
        {
            Debug.LogError($"[DB] Seed Database failed: {e.Message}\n{e}");
            EditorUtility.DisplayDialog("Database", $"FAILED\n\n{e.Message}", "OK");
        }
    }

    [MenuItem("Tools/Database/DAL Self-Test")]
    public static void DalSelfTest()
    {
        if (!Application.isPlaying || PersistenceService.Instance == null)
        {
            EditorUtility.DisplayDialog(
                "DAL Self-Test",
                "Start a Host first (enter Play mode and start a server/host). The self-test " +
                "exercises the live PersistenceService — its worker thread and main-thread pump.",
                "OK");
            return;
        }

        var svc  = PersistenceService.Instance;
        var repo = new DalSmokeRepository();

        // 1. Plain (non-keyed) writes.
        svc.EnqueueSave(new DelegateSaveJob((c, tx) =>
            repo.Upsert(c, tx, new DalSmokeSnapshot { Id = "selftest:A", Payload = "a" })));
        svc.EnqueueSave(new DelegateSaveJob((c, tx) =>
            repo.Upsert(c, tx, new DalSmokeSnapshot { Id = "selftest:B", Payload = "b" })));

        // 2. Coalesced writes for one key — only the latest ("v2") should persist.
        svc.EnqueueSave(new KeyedDelegateSaveJob("selftest:D", (c, tx) =>
            repo.Upsert(c, tx, new DalSmokeSnapshot { Id = "selftest:D", Payload = "v1" })));
        svc.EnqueueSave(new KeyedDelegateSaveJob("selftest:D", (c, tx) =>
            repo.Upsert(c, tx, new DalSmokeSnapshot { Id = "selftest:D", Payload = "v2" })));

        // 3. A row for the async-load path.
        svc.EnqueueSave(new DelegateSaveJob((c, tx) =>
            repo.Upsert(c, tx, new DalSmokeSnapshot { Id = "selftest:L", Payload = "loadme" })));

        // 4. Verification runs after all the above (FIFO on the worker), then exercises LoadAsync.
        svc.EnqueueSave(new DelegateSaveJob((c, tx) =>
        {
            var a = repo.Load(c, "selftest:A", tx);
            var b = repo.Load(c, "selftest:B", tx);
            var d = repo.Load(c, "selftest:D", tx);
            bool ok = a == "a" && b == "b" && d == "v2";
            Debug.Log(ok
                ? "[DB] DAL Self-Test — write queue + coalescing PASS (D coalesced to 'v2')."
                : $"[DB] DAL Self-Test — write/coalesce FAIL (A={a}, B={b}, D={d}).");

            svc.RunOnMainThread(() =>
                svc.LoadAsync(conn => repo.Load(conn, "selftest:L"), payload =>
                {
                    Debug.Log(payload == "loadme"
                        ? "[DB] DAL Self-Test — LoadAsync + main-thread marshaling PASS."
                        : $"[DB] DAL Self-Test — LoadAsync FAIL (got '{payload ?? "null"}').");

                    // Clean up all self-test rows.
                    svc.EnqueueSave(new DelegateSaveJob((cc, ttx) =>
                    {
                        repo.Delete(cc, "selftest:A", ttx);
                        repo.Delete(cc, "selftest:B", ttx);
                        repo.Delete(cc, "selftest:D", ttx);
                        repo.Delete(cc, "selftest:L", ttx);
                        Debug.Log("[DB] DAL Self-Test — cleanup done.");
                    }));
                }));
        }));

        Debug.Log("[DB] DAL Self-Test enqueued — watch the Console for PASS/FAIL over the next moment.");
    }

    [MenuItem("Tools/Database/Create Account")]
    public static void CreateAccount() => CreateAccountWindow.Open();

    [MenuItem("Tools/Database/Save Character Now")]
    public static void SaveCharacterNow()
    {
        if (!Application.isPlaying || PersistenceService.Instance == null)
        {
            EditorUtility.DisplayDialog("Save Character",
                "Start a Host first — this enqueues a save for every connected character through the live PersistenceService.",
                "OK");
            return;
        }
        var comps = UnityEngine.Object.FindObjectsByType<CharacterPersistence>(FindObjectsSortMode.None);
        foreach (var c in comps) c.Save();
        Debug.Log($"[DB] Save Character Now — enqueued {comps.Length} character save(s).");
    }

    [MenuItem("Tools/Database/Wipe Character (by account)")]
    public static void WipeCharacter() => WipeCharacterWindow.Open();

    // ── Content export/import (M2.11, SE2/SE3) ──────────────────────────────────────────
    // Generic, schema-driven — see ContentExportImport.cs. "Content" = every DB table except the
    // player/account-state ones (accounts, characters, character_*, schema_version).

    [MenuItem("Tools/Database/Export Content...")]
    public static void ExportContent()
    {
        string path = EditorUtility.SaveFilePanel(
            "Export Content", "", $"ueq-content-{DateTime.UtcNow:yyyyMMdd-HHmmss}.json", "json");
        if (string.IsNullOrEmpty(path)) return;

        try
        {
            using var conn = Database.OpenEditorConnection();
            int rows = ContentExportImport.ExportToFile(conn, path);
            Debug.Log($"[DB] Exported content to {path} ({rows} row(s) total).");
            EditorUtility.DisplayDialog("Export Content", $"Exported {rows} row(s) to:\n\n{path}", "OK");
        }
        catch (Exception e)
        {
            Debug.LogError($"[DB] Export Content failed: {e.Message}\n{e}");
            EditorUtility.DisplayDialog("Export Content", $"FAILED\n\n{e.Message}", "OK");
        }
    }

    [MenuItem("Tools/Database/Import Content...")]
    public static void ImportContent()
    {
        string path = EditorUtility.OpenFilePanel("Import Content", "", "json");
        if (string.IsNullOrEmpty(path)) return;

        if (!EditorUtility.DisplayDialog("Import Content",
                "This REPLACES ALL content (items, mobs, abilities, races, classes, factions, " +
                "conversations, loot, vendors, spawn tables, …) in the currently configured database " +
                "with the contents of this file. Player accounts and characters are untouched. " +
                "This cannot be undone.\n\nContinue?", "Import (Replace All Content)", "Cancel"))
            return;

        try
        {
            using var conn = Database.OpenEditorConnection();
            ContentExportImport.ImportFromFile(conn, path);
            Debug.Log($"[DB] Imported content from {path}.");
            EditorUtility.DisplayDialog("Import Content",
                "Import complete. Restart the Unity host to load the new content.", "OK");
        }
        catch (Exception e)
        {
            Debug.LogError($"[DB] Import Content failed: {e.Message}\n{e}");
            EditorUtility.DisplayDialog("Import Content", $"FAILED — no changes were committed.\n\n{e.Message}", "OK");
        }
    }

    [MenuItem("Tools/Database/Reset (drop schema_version)")]
    public static void Reset()
    {
        if (!EditorUtility.DisplayDialog(
                "Database Reset",
                "Drop schema_version? Re-run migrations afterward to rebuild it. Dev only.",
                "Drop", "Cancel"))
            return;
        try
        {
            using var conn = Database.OpenEditorConnection();
            using var cmd = new NpgsqlCommand("DROP TABLE IF EXISTS schema_version;", conn);
            cmd.ExecuteNonQuery();
            Debug.Log("[DB] Dropped schema_version.");
            EditorUtility.DisplayDialog("Database", "Dropped schema_version.", "OK");
        }
        catch (Exception e)
        {
            Debug.LogError($"[DB] Reset failed: {e.Message}\n{e}");
            EditorUtility.DisplayDialog("Database", $"FAILED\n\n{e.Message}", "OK");
        }
    }
}

/// <summary>Minimal editor window to seed an account (1.4) — hashes the password and inserts a row.</summary>
public class CreateAccountWindow : EditorWindow
{
    string _username = "";
    string _password = "";

    public static void Open()
    {
        var w = GetWindow<CreateAccountWindow>(true, "Create Account");
        w.minSize = new Vector2(320, 130);
        w.Show();
    }

    void OnGUI()
    {
        EditorGUILayout.LabelField("Create a login account", EditorStyles.boldLabel);
        _username = EditorGUILayout.TextField("Username", _username);
        _password = EditorGUILayout.PasswordField("Password", _password);
        EditorGUILayout.Space();

        using (new EditorGUI.DisabledScope(_username.Trim().Length < 3 || _password.Length < 4))
        {
            if (GUILayout.Button("Create"))
                Create(_username.Trim().ToLowerInvariant(), _password);
        }
    }

    void Create(string username, string password)
    {
        try
        {
            using var conn = Database.OpenEditorConnection();
            long? id = new AccountRepository().TryRegister(conn, username, PasswordHasher.Hash(password));
            if (id == null)
            {
                EditorUtility.DisplayDialog("Create Account", $"Username '{username}' is already taken.", "OK");
                return;
            }
            Debug.Log($"[DB] Created account #{id} ({username}).");
            EditorUtility.DisplayDialog("Create Account", $"Created account #{id} ({username}).", "OK");
            Close();
        }
        catch (Exception e)
        {
            Debug.LogError($"[DB] Create Account failed: {e.Message}\n{e}");
            EditorUtility.DisplayDialog("Create Account", $"FAILED\n\n{e.Message}", "OK");
        }
    }
}

/// <summary>Editor window to delete a character by account username (children cascade) — 1.3 testing.</summary>
public class WipeCharacterWindow : EditorWindow
{
    string _username = "";

    public static void Open()
    {
        var w = GetWindow<WipeCharacterWindow>(true, "Wipe Character");
        w.minSize = new Vector2(320, 110);
        w.Show();
    }

    void OnGUI()
    {
        EditorGUILayout.LabelField("Delete a character by account username", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Removes the characters row (inventory/equipment/faction/hotbar cascade). The account itself is kept.", MessageType.Warning);
        _username = EditorGUILayout.TextField("Username", _username);

        using (new EditorGUI.DisabledScope(_username.Trim().Length < 3))
        {
            if (GUILayout.Button("Wipe"))
                Wipe(_username.Trim().ToLowerInvariant());
        }
    }

    void Wipe(string username)
    {
        try
        {
            using var conn = Database.OpenEditorConnection();
            var account = new AccountRepository().FindByUsername(conn, username);
            if (account == null)
            {
                EditorUtility.DisplayDialog("Wipe Character", $"No account '{username}'.", "OK");
                return;
            }
            int rows = new CharacterRepository().DeleteByAccount(conn, account.Value.id);
            Debug.Log($"[DB] Wiped {rows} character(s) for account '{username}' (#{account.Value.id}).");
            EditorUtility.DisplayDialog("Wipe Character",
                rows > 0 ? $"Deleted character for '{username}'." : $"No character existed for '{username}'.", "OK");
            Close();
        }
        catch (Exception e)
        {
            Debug.LogError($"[DB] Wipe Character failed: {e.Message}\n{e}");
            EditorUtility.DisplayDialog("Wipe Character", $"FAILED\n\n{e.Message}", "OK");
        }
    }
}
