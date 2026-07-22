using System;
using System.IO;
using UnityEngine;

/// <summary>
/// Resolved Postgres connection settings for the server process.
/// </summary>
public struct DbSettings
{
    public string ConnectionString;
    public bool   SeedOnStart;
}

/// <summary>
/// Resolves the server's Postgres settings. Resolution order:
///   1. UEQ_DB_CONNSTRING env var (full connection string)
///   2. individual UEQ_DB_HOST/PORT/NAME/USER/PASSWORD env vars
///   3. db.config.json at the project root (editor / local-dev convenience)
/// Secrets never live in git: <c>.env</c> feeds docker, <c>db.config.json</c> feeds the
/// editor, and a standalone server reads env vars set by its host (AWS later).
/// </summary>
public static class DatabaseConfig
{
    [Serializable]
    class FileConfig
    {
        public string host = "localhost";
        public int    port = 5432;
        public string database = "ueq";
        public string username = "ueq";
        public string password = "";
        public bool   seedOnStart = true;
    }

    // Multi-target format: db.config.json can define several named profiles (e.g. "local" vs.
    // "remote-tunnel") and pick one via "active" — switching targets is then a one-line edit
    // instead of hand-editing host/port/password each time. The legacy flat single-object format
    // (no "active"/"profiles") is still supported below, for backward compatibility.
    [Serializable]
    class NamedProfile : FileConfig
    {
        public string name;
    }

    [Serializable]
    class ProfileFile
    {
        public string active;
        public NamedProfile[] profiles;
    }

    public static DbSettings Resolve()
    {
        var full = Environment.GetEnvironmentVariable("UEQ_DB_CONNSTRING");
        if (!string.IsNullOrEmpty(full))
            return new DbSettings { ConnectionString = full, SeedOnStart = EnvSeed() };

        var host = Environment.GetEnvironmentVariable("UEQ_DB_HOST");
        if (!string.IsNullOrEmpty(host))
        {
            var port = Environment.GetEnvironmentVariable("UEQ_DB_PORT")     ?? "5432";
            var db   = Environment.GetEnvironmentVariable("UEQ_DB_NAME")     ?? "ueq";
            var user = Environment.GetEnvironmentVariable("UEQ_DB_USER")     ?? "ueq";
            var pass = Environment.GetEnvironmentVariable("UEQ_DB_PASSWORD") ?? "";
            return new DbSettings
            {
                ConnectionString = $"Host={host};Port={port};Database={db};Username={user};Password={pass}",
                SeedOnStart      = EnvSeed()
            };
        }

        var cfg = LoadFileConfig();
        return new DbSettings
        {
            ConnectionString =
                $"Host={cfg.host};Port={cfg.port};Database={cfg.database};" +
                $"Username={cfg.username};Password={cfg.password}",
            SeedOnStart = cfg.seedOnStart
        };
    }

    static bool EnvSeed()
    {
        var v = Environment.GetEnvironmentVariable("UEQ_DB_SEED");
        return v == null || v == "1" || v.Equals("true", StringComparison.OrdinalIgnoreCase);
    }

    static FileConfig LoadFileConfig()
    {
        var path = ProjectRootFile("db.config.json");
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                "No DB config found. Set UEQ_DB_* env vars or create db.config.json at the " +
                "project root (copy db.config.example.json). Looked at: " + path);
        }

        var text = File.ReadAllText(path);

        var multi = JsonUtility.FromJson<ProfileFile>(text);
        if (multi != null && !string.IsNullOrEmpty(multi.active) && multi.profiles != null)
        {
            foreach (var p in multi.profiles)
                if (p.name == multi.active)
                    return p;
            throw new InvalidOperationException(
                $"db.config.json: active profile '{multi.active}' not found among: " +
                string.Join(", ", Array.ConvertAll(multi.profiles, p => p.name)));
        }

        // Legacy flat single-object format.
        return JsonUtility.FromJson<FileConfig>(text);
    }

    static string ProjectRootFile(string fileName)
    {
        // Application.dataPath is <project>/Assets in the editor.
        var root = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
        return Path.Combine(root, fileName);
    }
}
