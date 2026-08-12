using Npgsql;

namespace Darge.Web.Data;

public static class Database
{
    private static readonly string ConnectionString =
        Environment.GetEnvironmentVariable("DARGE_DB_CONNECTION")
        ?? throw new InvalidOperationException(
            "DARGE_DB_CONNECTION environment variable is required. Please set DARGE_DB_CONNECTION to your PostgreSQL connection string."
        );

    public static readonly NpgsqlDataSource DataSource =
        NpgsqlDataSource.Create(ConnectionString);

    public static void InitializeTables()
    {
        using var connection = DataSource.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            -- 1. Users Table
            CREATE TABLE IF NOT EXISTS users (
                id SERIAL PRIMARY KEY,
                username VARCHAR(100) NOT NULL UNIQUE,
                password_hash TEXT NOT NULL,
                password_salt TEXT NOT NULL DEFAULT '',
                xp INT DEFAULT 0
            );

            ALTER TABLE users ADD COLUMN IF NOT EXISTS password_salt TEXT DEFAULT '';
            ALTER TABLE users ADD COLUMN IF NOT EXISTS xp INT DEFAULT 0;

            -- 2. Projects Table
            CREATE TABLE IF NOT EXISTS projects (
                id SERIAL PRIMARY KEY,
                user_id INT NOT NULL REFERENCES users(id) ON DELETE CASCADE,
                plant_name VARCHAR(100) NOT NULL,
                location VARCHAR(100),
                soil_type VARCHAR(100),
                space DOUBLE PRECISION DEFAULT 0
            );

            -- 3. Project Tasks Table
            CREATE TABLE IF NOT EXISTS project_tasks (
                id SERIAL PRIMARY KEY,
                project_id INT NOT NULL REFERENCES projects(id) ON DELETE CASCADE,
                task_name VARCHAR(200) NOT NULL,
                task_description TEXT,
                task_level VARCHAR(50) DEFAULT 'Easy',
                is_completed BOOLEAN DEFAULT FALSE,
                xp_reward INT DEFAULT 10
            );

            -- 4. User Achievements Table
            CREATE TABLE IF NOT EXISTS user_achievements (
                id SERIAL PRIMARY KEY,
                user_id INT NOT NULL REFERENCES users(id) ON DELETE CASCADE,
                achievement_name VARCHAR(100) NOT NULL,
                unlocked_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
            );

            ALTER TABLE user_achievements ADD COLUMN IF NOT EXISTS achievement_name VARCHAR(100);
            
            -- Ensure UNIQUE index for ON CONFLICT (user_id, achievement_name)
            CREATE UNIQUE INDEX IF NOT EXISTS idx_user_achievements_user_name 
            ON user_achievements(user_id, achievement_name);

            -- 5. AI Conversations Table
            CREATE TABLE IF NOT EXISTS ai_conversations (
                id SERIAL PRIMARY KEY,
                user_id INT NOT NULL REFERENCES users(id) ON DELETE CASCADE,
                project_id INT REFERENCES projects(id) ON DELETE CASCADE,
                title VARCHAR(200) NOT NULL,
                created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
            );

            -- 6. AI Messages Table
            CREATE TABLE IF NOT EXISTS ai_messages (
                id SERIAL PRIMARY KEY,
                conversation_id INT NOT NULL REFERENCES ai_conversations(id) ON DELETE CASCADE,
                role VARCHAR(50) NOT NULL,
                content TEXT NOT NULL,
                created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
            );
            """;
        command.ExecuteNonQuery();
    }
}