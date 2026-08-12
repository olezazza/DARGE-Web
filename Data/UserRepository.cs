using System.Security.Cryptography;
using Darge.Web.Models;

namespace Darge.Web.Data;

public static class UserRepository
{
    public static User? Login(
        string username,
        string password
    )
    {
        using var command =
            Database.DataSource.CreateCommand(
                """
                SELECT
                    id,
                    username,
                    password_hash,
                    password_salt,
                    xp
                FROM users
                WHERE LOWER(username) = LOWER($1);
                """
            );

        command.Parameters.AddWithValue(username);

        using var reader =
            command.ExecuteReader();

        if (!reader.Read())
        {
            return null;
        }


        int id =
            reader.GetInt32(0);

        string databaseUsername =
            reader.GetString(1);

        string storedHashString =
            reader.GetString(2);

        string storedSaltString =
            reader.GetString(3);

        int xp =
            reader.GetInt32(4);


        byte[] storedHash =
            Convert.FromBase64String(
                storedHashString
            );

        byte[] salt =
            Convert.FromBase64String(
                storedSaltString
            );


        byte[] enteredHash =
            Rfc2898DeriveBytes.Pbkdf2(
                password,
                salt,
                100000,
                HashAlgorithmName.SHA256,
                32
            );


        bool correct =
            CryptographicOperations.FixedTimeEquals(
                enteredHash,
                storedHash
            );


        if (!correct)
        {
            return null;
        }


        return new User
        {
            Id = id,
            Username = databaseUsername,
            Xp = xp
        };
    }


    public static User? Register(
        string username,
        string password
    )
    {
        if (UsernameExists(username))
        {
            return null;
        }


        byte[] salt =
            RandomNumberGenerator.GetBytes(16);


        byte[] hash =
            Rfc2898DeriveBytes.Pbkdf2(
                password,
                salt,
                100000,
                HashAlgorithmName.SHA256,
                32
            );


        string saltString =
            Convert.ToBase64String(salt);

        string hashString =
            Convert.ToBase64String(hash);


        using var command =
            Database.DataSource.CreateCommand(
                """
                INSERT INTO users
                (
                    username,
                    password_hash,
                    password_salt
                )
                VALUES ($1, $2, $3)
                RETURNING id, xp;
                """
            );


        command.Parameters.AddWithValue(
            username
        );

        command.Parameters.AddWithValue(
            hashString
        );

        command.Parameters.AddWithValue(
            saltString
        );


        using var reader =
            command.ExecuteReader();


        reader.Read();


        return new User
        {
            Id = reader.GetInt32(0),
            Username = username,
            Xp = reader.GetInt32(1)
        };
    }


    public static bool UsernameExists(
        string username
    )
    {
        using var command =
            Database.DataSource.CreateCommand(
                """
                SELECT COUNT(*)
                FROM users
                WHERE LOWER(username) = LOWER($1);
                """
            );


        command.Parameters.AddWithValue(
            username
        );


        long count =
            Convert.ToInt64(
                command.ExecuteScalar()
            );


        return count > 0;
    }
}