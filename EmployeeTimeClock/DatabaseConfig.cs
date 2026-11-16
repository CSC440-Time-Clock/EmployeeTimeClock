using System;

namespace EmployeeTimeClock
{
    // MariaDB connection settings -H
    internal static class DatabaseConfig
    {
        // database host name -H
        internal const string Server = "csitmariadb.eku.edu";

        // port -H
        internal const int Port = 3306;

        // database login credentials -H
        internal const string Username = "student";
        internal const string Password = "Maroon@21?";

        // db name -H
        internal const string DatabaseName = "csc340_db";
    }
}