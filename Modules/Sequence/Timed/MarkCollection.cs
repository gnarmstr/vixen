﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Runtime.Serialization;
using Common.Controls.TimelineControl;

namespace VixenModules.Sequence.Timed
{
	[DataContract]
	public class MarkCollection
	{
		public MarkCollection()
		{
			Marks = new List<TimeSpan>();
			LabeledMarks = new List<Mark>();
			Id = Guid.NewGuid();
			MarkColor = Color.Black;
			Level = 1;
			Enabled = true;
			Bold = false;
			SolidLine = false;
		}

		public MarkCollection(MarkCollection original)
		{
			Marks = new List<TimeSpan>(original.Marks);
			LabeledMarks = original.LabeledMarks.ToList();
			Id = Guid.NewGuid();
			MarkColor = original.MarkColor;
			Level = original.Level;
			Enabled = original.Enabled;
			Bold = original.Bold;
			Name = original.Name;
			SolidLine = original.SolidLine;
		}

		[DataMember]
		public string Name { get; set; }

		[DataMember]
		public bool Enabled { get; set; }

		[DataMember]
		public bool Bold { get; set; }

		[DataMember]
		public bool SolidLine { get; set; }

		[DataMember]
		public List<TimeSpan> Marks { get; set; }

		[DataMember]
		public List<Mark> LabeledMarks { get; set; }

		[DataMember]
		public Color MarkColor { get; set; }

		[DataMember]
		public int Level { get; set; }

		[DataMember]
		public Guid Id { get; set; }

		public int MarkCount
		{
			get { return Marks.Count; }
		}

		public int IndexOf(TimeSpan time)
		{
			return Marks.IndexOf(time);
		}

		public void ConvertMarksToLabeledMarks()
		{
			//Temp method to convert until existing code is refactored
			LabeledMarks = Marks.Select(x => new Mark(x)).ToList();
		}

		[OnDeserialized]
		public void OnDeserialized(StreamingContext context)
		{
			//if (LabeledMarks == null)
			//{
				LabeledMarks = Marks.Select(x => new Mark(x)).ToList();
			//}
		}
	}
}