namespace Buscarro.Tests;

public sealed class PostgresTestDatabaseFixtureTests
{
    [Fact]
    public void Guard_requires_explicit_run_test_database_name_and_remote_opt_in()
    {
        Assert.False(PostgresTestDatabaseFixture.Validate("Host=localhost;Database=buscarro_test","false",null,out _));
        Assert.False(PostgresTestDatabaseFixture.Validate("Host=localhost;Database=buscarro","true",null,out _));
        Assert.True(PostgresTestDatabaseFixture.Validate("Host=localhost;Database=buscarro_test","true",null,out _));
        Assert.True(PostgresTestDatabaseFixture.Validate("Host=LOCALHOST;Database=buscarro_test","true",null,out _));
        Assert.False(PostgresTestDatabaseFixture.Validate("Host=db.example;Database=buscarro_test","true",null,out _));
        Assert.True(PostgresTestDatabaseFixture.Validate("Host=db.example;Database=buscarro_test","true","true",out _));
    }
}
