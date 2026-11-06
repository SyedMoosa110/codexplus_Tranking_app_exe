using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace WorkAgent.Storage;

public sealed class LocalQueue
{
    private readonly string _connectionString;

    public LocalQueue(string dbPath)
    {
        var folder = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(folder))
        {
            Directory.CreateDirectory(folder);
        }

        _connectionString = $"Data Source={dbPath}";
        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        using var command = new SqliteCommand(
            @"CREATE TABLE IF NOT EXISTS event_queue (
                id TEXT PRIMARY KEY,
                created_at TEXT NOT NULL,
                json TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_event_queue_created ON event_queue(created_at);",
            connection
        );
        command.ExecuteNonQuery();
    }

    public void Enqueue(Guid id, object payload)
    {
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var command = new SqliteCommand(
                "INSERT OR IGNORE INTO event_queue (id, created_at, json) VALUES ($id, $created_at, $json)",
                connection
            );
            command.Parameters.AddWithValue("$id", id.ToString());
            command.Parameters.AddWithValue("$created_at", DateTime.UtcNow.ToString("o"));
            command.Parameters.AddWithValue("$json", JsonSerializer.Serialize(payload));
            command.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[LocalQueue] Enqueue error: {ex.Message}");
        }
    }

    public List<(string Id, string Json)> Peek(int max = 300)
    {
        var list = new List<(string, string)>();
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var command = new SqliteCommand(
                "SELECT id, json FROM event_queue ORDER BY created_at ASC LIMIT $limit",
                connection
            );
            command.Parameters.AddWithValue("$limit", max);

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                list.Add((reader.GetString(0), reader.GetString(1)));
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[LocalQueue] Peek error: {ex.Message}");
        }
        return list;
    }

    public void Delete(IEnumerable<string> ids)
    {
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var transaction = connection.BeginTransaction();

            using var command = new SqliteCommand(
                "DELETE FROM event_queue WHERE id = $id",
                connection,
                transaction
            );
            var idParam = command.Parameters.Add("$id", SqliteType.Text);

            foreach (var id in ids)
            {
                idParam.Value = id;
                command.ExecuteNonQuery();
            }

            transaction.Commit();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[LocalQueue] Delete error: {ex.Message}");
        }
    }

    public int GetPendingCount()
    {
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();
            using var command = new SqliteCommand("SELECT COUNT(*) FROM event_queue", connection);
            return Convert.ToInt32(command.ExecuteScalar());
        }
        catch
        {
            return 0;
        }
    }
}
