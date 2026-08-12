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
}