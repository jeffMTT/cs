/****************************************************************************************************
	Michael J Swart
	http://michaeljswart.com/2017/02/a-program-to-find-insert-statements-that-dont-specify-columns/
	Program to find INSERT statements that do not specify columns
*****************************************************************************************************/

using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using Microsoft.SqlServer.TransactSql.ScriptDom;
 
class Program {
 
    static void Main(string[] args) {
 
        SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder {
            DataSource = ".",
            InitialCatalog = "test_database",
            IntegratedSecurity = true
        };
 
        using (SqlConnection conn = new SqlConnection(builder.ToString())) {
            conn.Open();
            SqlCommand command = new SqlCommand(@"
                SELECT OBJECT_SCHEMA_NAME(object_id) [schema], 
                       OBJECT_NAME(object_id)        [procedure], 
                       OBJECT_DEFINITION(object_id)  [sql]
                  FROM sys.procedures 
                 ORDER BY OBJECT_SCHEMA_NAME(object_id), OBJECT_NAME(object_id) ;", conn);
            SqlDataReader reader = command.ExecuteReader();
            while (reader.Read()) {
                string schema = reader["schema"].ToString();
                string procedure = reader["procedure"].ToString();
                string sql = reader["sql"].ToString();
                if (SqlHasInsertWithoutColumnList(sql)) {
                    Console.WriteLine( $"{schema}.{procedure}" );
                }
            }
        }            
    }
 
    static bool SqlHasInsertWithoutColumnList(string SQL) {
        SQLVisitor visitor = new SQLVisitor();
        // visitor.TolerateTempTables = true;
        TSql130Parser parser = new TSql130Parser(true);
        IList<ParseError> errors;
        var fragment = parser.Parse(new System.IO.StringReader(SQL), out errors);
        fragment.Accept(visitor);
        return visitor.HasInsertWithoutColumnSpecification;
    }
}
 
internal class SQLVisitor : TSqlFragmentVisitor {
    public bool HasInsertWithoutColumnSpecification { get; set; }
    public bool TolerateTempTables { get; set; }
 
    public override void ExplicitVisit(InsertStatement node) {
        if (node.InsertSpecification.Columns.Any())
            return;
 
        var source = node.InsertSpecification.InsertSource as ValuesInsertSource;
        if (source != null && source.IsDefaultValues)
            return;
 
        if (TolerateTempTables) {
            var target = node.InsertSpecification.Target as NamedTableReference;
            if (target != null && !target.SchemaObject.BaseIdentifier.Value.StartsWith("#")) {
                HasInsertWithoutColumnSpecification = true;
            }
        } else {
            HasInsertWithoutColumnSpecification = true;
        }
    }
}