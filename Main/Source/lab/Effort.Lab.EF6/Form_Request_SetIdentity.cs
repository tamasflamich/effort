using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Data.Common;
using System.Data.Entity;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Effort.Lab.EF6
{
    public partial class Form_Request_SetIdentity : Form
    {
        public Form_Request_SetIdentity()
        {
            InitializeComponent();

            var connection = Effort.DbConnectionFactory.CreateTransient(); 


            // CLEAN
            using (var context = new EntityContext(connection))
            {
                context.Database.CreateIfNotExists();
                connection.Open();

                connection.DbManager.SetIdentity<EntitySimple>(50, 1);
                connection.DbManager.SetIdentity<EntitySimple2>(100, 2);
                connection.Close();
                context.EntitySimples.RemoveRange(context.EntitySimples);
                context.SaveChanges();
            }

            // SEED
            using (var context = new EntityContext(connection))
            {
                context.EntitySimples.Add(new EntitySimple { ColumnInt = 1 });
                context.EntitySimples.Add(new EntitySimple { ColumnInt = 2 });
                context.EntitySimples.Add(new EntitySimple { ColumnInt = 3 });
                context.SaveChanges();
            }

            // SEED
            using (var context = new EntityContext(connection))
            {
                context.EntitySimple2s.Add(new EntitySimple2 { ColumnInt = 1 });
                context.EntitySimple2s.Add(new EntitySimple2 { ColumnInt = 2 });
                context.EntitySimple2s.Add(new EntitySimple2 { ColumnInt = 3 });
                context.SaveChanges();
            }

            // TEST
            using (var context = new EntityContext(connection))
            {
                var list = context.EntitySimples.ToList();
                var list2 = context.EntitySimple2s.ToList();
            }
        }

        public class EntityContext : DbContext
        {
            public EntityContext(DbConnection connection) : base(connection, true)
            {
            }

            public DbSet<EntitySimple> EntitySimples { get; set; }
            public DbSet<EntitySimple2> EntitySimple2s { get; set; }


            protected override void OnModelCreating(DbModelBuilder modelBuilder)
            {
                base.OnModelCreating(modelBuilder);
            }
        }

        [Table("patate")]
        public class EntitySimple
        {
            public int ID { get; set; }
            public int ColumnInt { get; set; }
        }
         
        public class EntitySimple2
        {
            public int ID { get; set; }
            public int ColumnInt { get; set; }
        }
    }
}