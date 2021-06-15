using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web;
using Cargo;

namespace CargoExample
{
    public class MyDataContext : DbContext 
    {
        public MyDataContext() : base("name=DefaultConnection")
        {

        }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            modelBuilder.MapCargoContent();
        }
    }
}