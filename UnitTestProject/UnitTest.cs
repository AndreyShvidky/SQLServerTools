using ObjectDependencyExplorer;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;

namespace UnitTestProject
{
    [TestClass]
    public class UnitTest
    {
        [TestMethod]
        public void TestMethodInsert()
        {
            string query = @"insert into omr.SyncTable
	(TableObjectID)
select
	SO.[object_id]
from
	sys.objects as SO
	left join omr.SyncTable as T on
		T.TableObjectID = SO.[object_id]
where
	T.TableID is null";

            SQLModule module = new SQLModule("test", "SomeProcedure", query);
            module.Parse(null);
            SQLObjectReference updatedTable = module.Dependencies.Where(it => it.ReferenceType == SQLStatement.SQLStatementType.Insert).FirstOrDefault();
            if (updatedTable?.FullName != "omr.SyncTable")
            {
                string message = "Пополняемая таблица omr.SyncTable определена неверно.";
                foreach (ParseMessage item in module.Messages)
                    message += "\t" + item.Message;
                throw (new Exception(message));
            }
        }

		[TestMethod]
		public void TestMethodUpdateDirect()
		{
			string query = @"update dbo.SomeTable
set
	[Name] = ''
where
	0 = 1";

			SQLModule module = new SQLModule("test", "SomeProcedure", query);
			module.Parse(null);
			SQLObjectReference updatedTable = module.Dependencies.Where(it => it.ReferenceType == SQLStatement.SQLStatementType.Update).FirstOrDefault();
			if (updatedTable?.FullName != "dbo.SomeTable")
			{
				string message = "Обновляемая таблица dbo.SomeTable определена неверно.";
				foreach (ParseMessage item in module.Messages)
					message += "\t" + item.Message;
				throw (new Exception(message));
			}
		}

		[TestMethod]
        public void TestMethodUpdateAlias()
        {
            string query = @"update SO
set
	name = ''
from
	omr.TableColumn as C
	inner join omr.SyncTable as T on
		T.TableID = C.TableID
	inner join sys.objects as SO on
		SO.[object_id] = T.TableObjectID
	inner join sys.columns as SC on
		SO.[object_id] = T.TableObjectID
		and SO.name = C.ColumnName";

            SQLModule module = new SQLModule("test", "SomeProcedure", query);
            module.Parse(null);
			SQLObjectReference updatedTable = module.Dependencies.Where(it => it.ReferenceType == SQLStatement.SQLStatementType.Update).FirstOrDefault();
            if (updatedTable?.FullName != "SO")
            {
                string message = "Обновляемая таблица sys.objects определена неверно.";
                foreach (ParseMessage item in module.Messages)
                    message += "\t" + item.Message;
                throw (new Exception(message));
            }
			updatedTable = module.Dependencies.Where(it => it.ReferenceType == SQLStatement.SQLStatementType.Update).Skip(1).FirstOrDefault();
			if (updatedTable?.FullName != "sys.objects")
			{
				string message = "Обновляемая таблица sys.objects определена неверно.";
				foreach (ParseMessage item in module.Messages)
					message += "\t" + item.Message;
				throw (new Exception(message));
			}
		}

		[TestMethod]
		public void TestMethodDeleteDirect()
		{
			string query = @"delete dbo.SomeTable
where
	 0 = 1";

			SQLModule module = new SQLModule("test", "SomeProcedure", query);
			module.Parse(null);
			SQLObjectReference deletedTable = module.Dependencies.Where(it => it.ReferenceType == SQLStatement.SQLStatementType.Delete).FirstOrDefault();
			if (deletedTable?.FullName != "dbo.SomeTable")
			{
				string message = "Удаляемая таблица dbo.SomeTable определена неверно.";
				foreach (ParseMessage item in module.Messages)
					message += "\t" + item.Message;
				throw (new Exception(message));
			}
		}

		[TestMethod]
		public void TestMethodDeleteDirectWithFrom()
		{
			string query = @"delete from dbo.SomeTable
where
	 0 = 1";

			SQLModule module = new SQLModule("test", "SomeProcedure", query);
			module.Parse(null);
			SQLObjectReference deletedTable = module.Dependencies.Where(it => it.ReferenceType == SQLStatement.SQLStatementType.Delete).FirstOrDefault();
			if (deletedTable?.FullName != "dbo.SomeTable")
			{
				string message = "Удаляемая таблица dbo.SomeTable определена неверно.";
				foreach (ParseMessage item in module.Messages)
					message += "\t" + item.Message;
				throw (new Exception(message));
			}
		}

		[TestMethod]
        public void TestMethodDeleteAlias()
        {
            string query = @"delete SO
from
	omr.TableColumn as C
	inner join omr.SyncTable as T on
		T.TableID = C.TableID
	inner join sys.objects as SO on
		SO.[object_id] = T.TableObjectID
	inner join sys.columns as SC on
		SO.[object_id] = T.TableObjectID
		and SO.name = C.ColumnName";

            SQLModule module = new SQLModule("test", "SomeProcedure", query);
            module.Parse(null);
			SQLObjectReference deletedTable = module.Dependencies.Where(it => it.ReferenceType == SQLStatement.SQLStatementType.Delete).FirstOrDefault();
            if (deletedTable?.FullName != "SO")
            {
                string message = "Удаляемая таблица SO определена неверно.";
                foreach (ParseMessage item in module.Messages)
                    message += "\t" + item.Message;
                throw (new Exception(message));
            }
			deletedTable = module.Dependencies.Where(it => it.ReferenceType == SQLStatement.SQLStatementType.Delete).Skip(1).FirstOrDefault();
			if (deletedTable?.FullName != "sys.objects")
			{
				string message = "Удаляемая таблица sys.objects определена неверно.";
				foreach (ParseMessage item in module.Messages)
					message += "\t" + item.Message;
				throw (new Exception(message));
			}

		}

		[TestMethod]
        public void TestMethodMerge()
        {
            string query = @"merge lan.Host as H
using @Host as pH on	-- Хост - у нас контейнер сохранения. Поэтому это первая и единственная сущность, соединение с которой производится по PK. С остальными по ключу предметной области.
	pH.HostID = H.HostID
when matched then update
set
	HostTypeID		= pH.HostTypeID
	,Name			= pH.[Name]
	,[Address]		= pH.[Address]
	,[Description]	= pH.[Description]
	,DeleteFlag		= pH.DeleteFlag
when not matched by target then
insert
	(HostTypeID, [Name], [Address], [Description], DeleteFlag)
values
	(pH.HostTypeID, pH.[Name], pH.[Address], pH.[Description], pH.DeleteFlag);";

            SQLModule module = new SQLModule("test", "SomeProcedure", query);
            module.Parse(null);
			SQLObjectReference mergedTable = module.Dependencies.Where(it => it.ReferenceType == SQLStatement.SQLStatementType.Merge).FirstOrDefault();
            if (mergedTable?.FullName != "lan.Host")
            {
                string message = "Удаляемая таблица sys.objects определена неверно.";
                foreach (ParseMessage item in module.Messages)
                    message += "\t" + item.Message;
                throw (new Exception(message));
            }
        }

        [TestMethod]
        public void TestMethodGeneral()
        {
            string query = @"declare @A int = 1
if @A = 1
begin
    select A = @A
end";

            SQLModule module = new SQLModule("test", "SomeProcedure", query);
            module.Parse(null);
            //if (module.Statements.Count != 2)
            //{
            //    throw (new Exception("Неверное число инструкций."));
            //}
        }
    }
}
