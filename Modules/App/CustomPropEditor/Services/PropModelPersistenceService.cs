﻿using System.Linq;
using LiteDB;
using VixenModules.App.CustomPropEditor.Model;

namespace VixenModules.App.CustomPropEditor.Services
{
	public class PropModelPersistenceService
	{
		public static bool SaveModel(Prop prop, string path)
		{
			using (var db = new LiteDatabase(path))
			{
				var col = db.GetCollection<Prop>("props");
				col.EnsureIndex(x => x.Name);
				col.Insert(prop);
			}

			return true;
		}

		public static bool UpdateModel(Prop prop, string path)
		{
			using (var db = new LiteDatabase(path))
			{
				var col = db.GetCollection<Prop>("props");
				col.EnsureIndex(x => x.Name);
				col.Update(prop);
			}

			return true;
		}

		public static Prop GetModel(string path)
		{
			Prop p;
			using (var db = new LiteDatabase(path))
			{
				var col = db.GetCollection<Prop>("props");

				p = col.FindAll().FirstOrDefault();
			}

			return p;
		}
	}
}
