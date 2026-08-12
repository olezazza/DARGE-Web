namespace Darge.Web.Data;

public static class AchievementRepository
{
    public static List<string> GetAchievements(
        int userId
    )
    {
        List<string> achievements =
            new();


        using var command =
            Database.DataSource.CreateCommand(
                """
                SELECT achievement_name

                FROM user_achievements

                WHERE user_id = $1

                ORDER BY unlocked_at;
                """
            );


        command.Parameters.AddWithValue(
            userId
        );


        using var reader =
            command.ExecuteReader();


        while (reader.Read())
        {
            achievements.Add(
                reader.GetString(0)
            );
        }


        return achievements;
    }


    public static void CheckAchievements(
        int userId
    )
    {
        int projects;
        int completedTasks;
        int xp;


        using var command =
            Database.DataSource.CreateCommand(
                """
                SELECT

                (
                    SELECT COUNT(*)
                    FROM projects
                    WHERE user_id = $1
                ),

                (
                    SELECT COUNT(*)

                    FROM project_tasks t

                    JOIN projects p
                    ON p.id = t.project_id

                    WHERE p.user_id = $1
                    AND t.is_completed = TRUE
                ),

                (
                    SELECT xp
                    FROM users
                    WHERE id = $1
                );
                """
            );


        command.Parameters.AddWithValue(
            userId
        );


        using var reader =
            command.ExecuteReader();


        if (!reader.Read())
        {
            return;
        }


        projects =
            Convert.ToInt32(
                reader.GetInt64(0)
            );


        completedTasks =
            Convert.ToInt32(
                reader.GetInt64(1)
            );


        xp =
            reader.GetInt32(2);


        reader.Close();


        if (projects >= 1)
        {
            Unlock(
                userId,
                "First Project"
            );
        }


        if (completedTasks >= 1)
        {
            Unlock(
                userId,
                "Getting Started"
            );
        }


        if (xp >= 100)
        {
            Unlock(
                userId,
                "100 XP"
            );
        }


        if (projects >= 3)
        {
            Unlock(
                userId,
                "Growing Collection"
            );
        }
    }


    private static void Unlock(
        int userId,
        string name
    )
    {
        using var command =
            Database.DataSource.CreateCommand(
                """
                INSERT INTO user_achievements
                (
                    user_id,
                    achievement_name
                )

                VALUES ($1, $2)

                ON CONFLICT
                (
                    user_id,
                    achievement_name
                )

                DO NOTHING;
                """
            );


        command.Parameters.AddWithValue(
            userId
        );

        command.Parameters.AddWithValue(
            name
        );


        command.ExecuteNonQuery();
    }
}