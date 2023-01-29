# Header
SQL Server object dependency explorer

# Main purpose
Explore SQL server changing dependencies, i.e. get answers on such questions as:
	- What tables this procedure deal with, and what operations it performs (select, insert, update or delete)
	- What procedures deal with this table, and what operations they performs (select, insert, update or delete)
	- What other procedures this procedure calls
	- What other procedures calls this procedure

Main SQL system views intended to help with this purpose
<br>
<i>select * from sys.sql_dependencies</i>
<br>
<i>select * from sys.sql_expression_dependencies</i>
<br>
gives very poor information, often incomplete.

# Main idea
With visitor pattern catch all visits in TableReference
https://learn.microsoft.com/en-us/dotnet/api/microsoft.sqlserver.transactsql.scriptdom.tablereference?view=sql-dacfx-161
and all its derived. Detect all referenced objects, including main targets: tables.

# Requirements
.NET 6.0

# Usage
<br>
<b>UI application</b>
<br>
<b>Console application</b>
<br>
Depending on authentication type:
<br>
For trusted (Windows):
<br>
<i>ObjectDependencyExplorerConsole -S {Server} -E -D {DataBase}</i>
<br>
For SQL:
<br>
<i>ObjectDependencyExplorerConsole -S {Server} -U {Login} -P {Password} -D {DataBase}</i>
<br>

# Results
Main result is the random named table in tempdb.
For example:
<br>
<i>select * from tempdb.dbo.SQL_DB_Module_Dependencies_bf642ea9_8903_4f8f_a128_22eb2890d60b</i>
<br>
This table is generated in console and in UI executables.

UI gives two tabs, where answes on two main questions can be obtained:

