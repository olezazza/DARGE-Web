using Darge.Web.Models;
using Npgsql;

namespace Darge.Web.Data;

public static class TaskRepository
{
    public static List<ProjectTask> GetTasks(
        int projectId
    )
    {
        List<ProjectTask> tasks = new();

        using var command =
            Database.DataSource.CreateCommand(
                """
                SELECT
                    id,
                    task_name,
                    task_description,
                    task_level,
                    is_completed,
                    xp_reward
                FROM project_tasks
                WHERE project_id = $1
                ORDER BY id;
                """
            );

        command.Parameters.AddWithValue(
            projectId
        );

        using var reader =
            command.ExecuteReader();

        while (reader.Read())
        {
            tasks.Add(
                new ProjectTask
                {
                    Id =
                        reader.GetInt32(0),

                    TaskName =
                        reader.GetString(1),

                    TaskDescription =
                        reader.IsDBNull(2)
                            ? ""
                            : reader.GetString(2),

                    TaskLevel =
                        reader.GetString(3),

                    IsCompleted =
                        reader.GetBoolean(4),

                    XpReward =
                        reader.GetInt32(5)
                }
            );
        }

        return tasks;
    }


    public static ProjectTask CreateTask(
        int projectId,
        int userId,
        string taskName,
        string taskDescription,
        string taskLevel
    )
    {
        int xpReward =
            taskLevel switch
            {
                "Easy" => 10,
                "Medium" => 20,
                "Hard" => 30,
                _ => 10
            };


        using var command =
            Database.DataSource.CreateCommand(
                """
                INSERT INTO project_tasks
                (
                    project_id,
                    task_name,
                    task_description,
                    task_level,
                    xp_reward
                )

                SELECT
                    id,
                    $3,
                    $4,
                    $5,
                    $6

                FROM projects

                WHERE id = $1
                AND user_id = $2

                RETURNING id;
                """
            );


        command.Parameters.AddWithValue(
            projectId
        );

        command.Parameters.AddWithValue(
            userId
        );

        command.Parameters.AddWithValue(
            taskName
        );

        command.Parameters.AddWithValue(
            taskDescription
        );

        command.Parameters.AddWithValue(
            taskLevel
        );

        command.Parameters.AddWithValue(
            xpReward
        );


        object? result =
            command.ExecuteScalar();


        if (result == null)
        {
            throw new Exception(
                "Project not found."
            );
        }


        return new ProjectTask
        {
            Id =
                Convert.ToInt32(result),

            TaskName =
                taskName,

            TaskDescription =
                taskDescription,

            TaskLevel =
                taskLevel,

            IsCompleted =
                false,

            XpReward =
                xpReward
        };
    }


    public static bool CompleteTask(
        int taskId,
        int userId
    )
    {
        using var connection =
            Database.DataSource.OpenConnection();


        using var transaction =
            connection.BeginTransaction();


        using var taskCommand =
            new NpgsqlCommand(
                """
                UPDATE project_tasks

                SET is_completed = TRUE

                WHERE id = $1

                AND is_completed = FALSE

                AND project_id IN
                (
                    SELECT id
                    FROM projects
                    WHERE user_id = $2
                )

                RETURNING xp_reward;
                """,
                connection,
                transaction
            );


        taskCommand.Parameters.AddWithValue(
            taskId
        );

        taskCommand.Parameters.AddWithValue(
            userId
        );


        object? result =
            taskCommand.ExecuteScalar();


        if (result == null)
        {
            transaction.Rollback();

            return false;
        }


        int xpReward =
            Convert.ToInt32(result);


        using var xpCommand =
            new NpgsqlCommand(
                """
                UPDATE users

                SET xp = xp + $1

                WHERE id = $2;
                """,
                connection,
                transaction
            );


        xpCommand.Parameters.AddWithValue(
            xpReward
        );

        xpCommand.Parameters.AddWithValue(
            userId
        );


        xpCommand.ExecuteNonQuery();


        transaction.Commit();


        return true;
    }


    public static void DeleteTask(
        int taskId,
        int userId
    )
    {
        using var command =
            Database.DataSource.CreateCommand(
                """
                DELETE FROM project_tasks

                WHERE id = $1

                AND project_id IN
                (
                    SELECT id
                    FROM projects
                    WHERE user_id = $2
                );
                """
            );


        command.Parameters.AddWithValue(
            taskId
        );

        command.Parameters.AddWithValue(
            userId
        );


        command.ExecuteNonQuery();
    }
}