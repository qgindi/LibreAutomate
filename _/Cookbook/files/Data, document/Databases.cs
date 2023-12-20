/// For SQL Server databases use NuGet package <+nuget>Microsoft.Data.SqlClient<>. For other databases look for other <google>NuGet</google> packages. Look for more info+examples on the internet.

/*/ nuget -\Microsoft.Data.SqlClient; /*/
using Microsoft.Data.SqlClient;

var connectionString = @"Data Source=(LocalDB)\MSSQLLocalDB;AttachDbFilename=C:\Test\SqlServer\Database1.mdf;Integrated Security=True";
using var con = new SqlConnection(connectionString);
con.Open();

var c1 = new SqlCommand("INSERT INTO [dbo].[Table] VALUES (2, 'text');", con);
c1.ExecuteNonQuery();

var c2 = new SqlCommand("SELECT Id,Name FROM [dbo].[Table];", con);
using (var r = c2.ExecuteReader()) {
	while (r.Read()) {
		print.it(r[0], r[1]);
	}
}
