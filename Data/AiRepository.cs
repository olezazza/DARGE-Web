using Npgsql;
using Darge.Web.Models;

namespace Darge.Web.Data;

public static class AiRepository
{
    public static AiConversation CreateConversation(int userId, int? projectId, string title)
    {
        string safeTitle = string.IsNullOrWhiteSpace(title) ? "New Conversation" : title.Trim();
        if (safeTitle.Length > 180) safeTitle = safeTitle.Substring(0, 180);

        using var command = Database.DataSource.CreateCommand(
            """
            INSERT INTO ai_conversations (user_id, project_id, title, created_at, updated_at)
            VALUES ($1, $2, $3, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP)
            RETURNING id, created_at, updated_at;
            """
        );

        command.Parameters.AddWithValue(userId);
        command.Parameters.AddWithValue(projectId.HasValue ? projectId.Value : DBNull.Value);
        command.Parameters.AddWithValue(safeTitle);

        using var reader = command.ExecuteReader();
        reader.Read();

        return new AiConversation
        {
            Id = reader.GetInt32(0),
            UserId = userId,
            ProjectId = projectId,
            Title = safeTitle,
            CreatedAt = reader.GetDateTime(1),
            UpdatedAt = reader.GetDateTime(2)
        };
    }

    public static List<AiConversation> GetUserConversations(int userId)
    {
        var conversations = new List<AiConversation>();

        using var command = Database.DataSource.CreateCommand(
            """
            SELECT id, user_id, project_id, title, created_at, updated_at
            FROM ai_conversations
            WHERE user_id = $1
            ORDER BY updated_at DESC;
            """
        );

        command.Parameters.AddWithValue(userId);

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            conversations.Add(new AiConversation
            {
                Id = reader.GetInt32(0),
                UserId = reader.GetInt32(1),
                ProjectId = reader.IsDBNull(2) ? null : reader.GetInt32(2),
                Title = reader.GetString(3),
                CreatedAt = reader.GetDateTime(4),
                UpdatedAt = reader.GetDateTime(5)
            });
        }

        return conversations;
    }

    public static List<AiConversation> GetConversations(int userId, int? projectId = null)
    {
        var conversations = new List<AiConversation>();

        using var command = Database.DataSource.CreateCommand(
            """
            SELECT id, user_id, project_id, title, created_at, updated_at
            FROM ai_conversations
            WHERE user_id = $1
            AND ($2::int IS NULL AND project_id IS NULL OR project_id = $2)
            ORDER BY updated_at DESC;
            """
        );

        command.Parameters.AddWithValue(userId);
        command.Parameters.AddWithValue(projectId.HasValue ? projectId.Value : DBNull.Value);

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            conversations.Add(new AiConversation
            {
                Id = reader.GetInt32(0),
                UserId = reader.GetInt32(1),
                ProjectId = reader.IsDBNull(2) ? null : reader.GetInt32(2),
                Title = reader.GetString(3),
                CreatedAt = reader.GetDateTime(4),
                UpdatedAt = reader.GetDateTime(5)
            });
        }

        return conversations;
    }

    public static AiConversation? GetConversation(int conversationId, int userId)
    {
        using var command = Database.DataSource.CreateCommand(
            """
            SELECT id, user_id, project_id, title, created_at, updated_at
            FROM ai_conversations
            WHERE id = $1 AND user_id = $2;
            """
        );

        command.Parameters.AddWithValue(conversationId);
        command.Parameters.AddWithValue(userId);

        using var reader = command.ExecuteReader();
        if (!reader.Read()) return null;

        var conv = new AiConversation
        {
            Id = reader.GetInt32(0),
            UserId = reader.GetInt32(1),
            ProjectId = reader.IsDBNull(2) ? null : reader.GetInt32(2),
            Title = reader.GetString(3),
            CreatedAt = reader.GetDateTime(4),
            UpdatedAt = reader.GetDateTime(5)
        };
        reader.Close();

        conv.Messages = GetMessages(conversationId);
        return conv;
    }

    public static List<AiMessage> GetMessages(int conversationId)
    {
        var messages = new List<AiMessage>();

        using var command = Database.DataSource.CreateCommand(
            """
            SELECT id, conversation_id, role, content, created_at
            FROM ai_messages
            WHERE conversation_id = $1
            ORDER BY id ASC;
            """
        );

        command.Parameters.AddWithValue(conversationId);

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            messages.Add(new AiMessage
            {
                Id = reader.GetInt32(0),
                ConversationId = reader.GetInt32(1),
                Role = reader.GetString(2),
                Content = reader.GetString(3),
                CreatedAt = reader.GetDateTime(4)
            });
        }

        return messages;
    }

    public static void AddMessage(int conversationId, string role, string content)
    {
        using var connection = Database.DataSource.OpenConnection();
        using var transaction = connection.BeginTransaction();

        using var msgCmd = new NpgsqlCommand(
            """
            INSERT INTO ai_messages (conversation_id, role, content, created_at)
            VALUES ($1, $2, $3, CURRENT_TIMESTAMP);
            """,
            connection,
            transaction
        );

        msgCmd.Parameters.AddWithValue(conversationId);
        msgCmd.Parameters.AddWithValue(role);
        msgCmd.Parameters.AddWithValue(content ?? "");
        msgCmd.ExecuteNonQuery();

        using var updateCmd = new NpgsqlCommand(
            """
            UPDATE ai_conversations
            SET updated_at = CURRENT_TIMESTAMP
            WHERE id = $1;
            """,
            connection,
            transaction
        );

        updateCmd.Parameters.AddWithValue(conversationId);
        updateCmd.ExecuteNonQuery();

        transaction.Commit();
    }

    public static void DeleteConversation(int conversationId, int userId)
    {
        using var command = Database.DataSource.CreateCommand(
            """
            DELETE FROM ai_conversations
            WHERE id = $1 AND user_id = $2;
            """
        );

        command.Parameters.AddWithValue(conversationId);
        command.Parameters.AddWithValue(userId);
        command.ExecuteNonQuery();
    }
}
