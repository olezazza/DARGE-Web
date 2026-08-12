using Npgsql;
using Darge.Web.Models;

namespace Darge.Web.Data;

public static class ProjectRepository
{
    public static Project CreateProject(
        int userId,
        string plantName,
        string location,
        string soilType,
        double space
    )
    {
        using var command =
            Database.DataSource.CreateCommand(
                """
                INSERT INTO projects
                (
                    user_id,
                    plant_name,
                    location,
                    soil_type,
                    space
                )
                VALUES ($1, $2, $3, $4, $5)
                RETURNING id;
                """
            );

        command.Parameters.AddWithValue(userId);
        command.Parameters.AddWithValue(plantName);
        command.Parameters.AddWithValue(location);
        command.Parameters.AddWithValue(soilType);
        command.Parameters.AddWithValue(space);

        int id =
            Convert.ToInt32(
                command.ExecuteScalar()
            );

        return new Project
        {
            Id = id,
            PlantName = plantName,
            Location = location,
            SoilType = soilType,
            Space = space
        };
    }


    public static List<Project> GetProjects(
        int userId
    )
    {
        List<Project> projects = new();

        using var command =
            Database.DataSource.CreateCommand(
                """
                SELECT
                    id,
                    plant_name,
                    location,
                    soil_type,
                    space
                FROM projects
                WHERE user_id = $1
                ORDER BY id DESC;
                """
            );

        command.Parameters.AddWithValue(userId);

        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                projects.Add(
                    new Project
                    {
                        Id = reader.GetInt32(0),

                        PlantName =
                            reader.GetString(1),

                        Location =
                            reader.IsDBNull(2)
                                ? ""
                                : reader.GetString(2),

                        SoilType =
                            reader.IsDBNull(3)
                                ? ""
                                : reader.GetString(3),

                        Space =
                            reader.GetDouble(4)
                    }
                );
            }
        }

        foreach (Project project in projects)
        {
            project.Tasks =
                TaskRepository.GetTasks(
                    project.Id
                );
        }

        return projects;
    }


    public static Project? GetProject(
        int projectId,
        int userId
    )
    {
        using var command =
            Database.DataSource.CreateCommand(
                """
                SELECT
                    id,
                    plant_name,
                    location,
                    soil_type,
                    space
                FROM projects
                WHERE id = $1
                AND user_id = $2;
                """
            );

        command.Parameters.AddWithValue(projectId);
        command.Parameters.AddWithValue(userId);

        using var reader =
            command.ExecuteReader();

        if (!reader.Read())
        {
            return null;
        }

        Project project =
            new Project
            {
                Id = reader.GetInt32(0),

                PlantName =
                    reader.GetString(1),

                Location =
                    reader.IsDBNull(2)
                        ? ""
                        : reader.GetString(2),

                SoilType =
                    reader.IsDBNull(3)
                        ? ""
                        : reader.GetString(3),

                Space =
                    reader.GetDouble(4)
            };

        reader.Close();

        project.Tasks =
            TaskRepository.GetTasks(
                project.Id
            );

        return project;
    }


    public static Project CreateProjectWithTasks(
        int userId,
        string plantName,
        string location,
        string soilType,
        double space,
        IEnumerable<AiTaskPlan> tasks
    )
    {
        using var connection =
            Database.DataSource.OpenConnection();

        using var transaction =
            connection.BeginTransaction();

        using var projCommand =
            new NpgsqlCommand(
                """
                INSERT INTO projects
                (user_id, plant_name, location, soil_type, space)
                VALUES ($1, $2, $3, $4, $5)
                RETURNING id;
                """,
                connection,
                transaction
            );

        projCommand.Parameters.AddWithValue(userId);
        projCommand.Parameters.AddWithValue(plantName);
        projCommand.Parameters.AddWithValue(location ?? "");
        projCommand.Parameters.AddWithValue(soilType ?? "");
        projCommand.Parameters.AddWithValue(space);

        int projectId = Convert.ToInt32(projCommand.ExecuteScalar());

        var createdTasks = new List<ProjectTask>();

        foreach (var task in tasks)
        {
            if (string.IsNullOrWhiteSpace(task.Title)) continue;

            string level = task.Difficulty switch
            {
                "Easy" or "easy" => "Easy",
                "Medium" or "medium" => "Medium",
                "Hard" or "hard" => "Hard",
                _ => "Easy"
            };

            int xp = level switch
            {
                "Easy" => 10,
                "Medium" => 20,
                "Hard" => 30,
                _ => 10
            };

            using var taskCommand =
                new NpgsqlCommand(
                    """
                    INSERT INTO project_tasks
                    (project_id, task_name, task_description, task_level, xp_reward)
                    VALUES ($1, $2, $3, $4, $5)
                    RETURNING id;
                    """,
                    connection,
                    transaction
                );

            taskCommand.Parameters.AddWithValue(projectId);
            taskCommand.Parameters.AddWithValue(task.Title);
            taskCommand.Parameters.AddWithValue(task.Description ?? "");
            taskCommand.Parameters.AddWithValue(level);
            taskCommand.Parameters.AddWithValue(xp);

            int taskId = Convert.ToInt32(taskCommand.ExecuteScalar());

            createdTasks.Add(new ProjectTask
            {
                Id = taskId,
                TaskName = task.Title,
                TaskDescription = task.Description ?? "",
                TaskLevel = level,
                IsCompleted = false,
                XpReward = xp
            });
        }

        transaction.Commit();

        return new Project
        {
            Id = projectId,
            PlantName = plantName,
            Location = location ?? "",
            SoilType = soilType ?? "",
            Space = space,
            Tasks = createdTasks
        };
    }


    public static void DeleteProject(
        int projectId,
        int userId
    )
    {
        using var connection =
            Database.DataSource.OpenConnection();

        using var transaction =
            connection.BeginTransaction();

        using var tasksCommand =
            new NpgsqlCommand(
                """
                DELETE FROM project_tasks
                WHERE project_id = $1
                AND project_id IN
                (
                    SELECT id FROM projects WHERE user_id = $2
                );
                """,
                connection,
                transaction
            );

        tasksCommand.Parameters.AddWithValue(projectId);
        tasksCommand.Parameters.AddWithValue(userId);
        tasksCommand.ExecuteNonQuery();

        using var projectCommand =
            new NpgsqlCommand(
                """
                DELETE FROM projects
                WHERE id = $1
                AND user_id = $2;
                """,
                connection,
                transaction
            );

        projectCommand.Parameters.AddWithValue(projectId);
        projectCommand.Parameters.AddWithValue(userId);
        projectCommand.ExecuteNonQuery();

        transaction.Commit();
    }
}