﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vixen.Sys;
using Vixen.Commands;
using Vixen.Commands.KnownDataTypes;
using Vixen.Module.Effect;
using VixenModules.App.ColorGradients;
using VixenModules.App.Curves;
using System.Drawing;

namespace VixenModules.Effect.Chase
{
	public class ChaseDescriptor : EffectModuleDescriptorBase
	{
		private static Guid _typeId = new Guid("{affea852-85b1-418f-9cdf-0b9735154bb5}");
		internal static Guid _RGBPropertyId = new Guid("{5c31be79-a6a7-4864-a660-4e0215ad4778}");
		private static Guid _CurvesId = new Guid("{4e258de2-7a75-4f0f-aa43-c8182e7f3400}");
		private static Guid _ColorGradientId = new Guid("{64f4ab26-3ed4-49a3-a004-23656ed0424a}");
		private static Guid _PulseId = new Guid("{cbd76d3b-c924-40ff-bad6-d1437b3dbdc0}");

		public override string EffectName { get { return "Chase"; } }

		public override Guid TypeId { get { return _typeId; } }

		public override Type ModuleClass { get { return typeof(Chase); } }

		public override Type ModuleDataClass { get { return typeof(ChaseData); } }

		public override string Author { get { return "Vixen Team"; } }

		public override string TypeName { get { return EffectName; } }

		public override string Description { get { return "Applies a pulse on consecutive channels in the given group, chasing though each item in the group."; } }

		public override string Version { get { return "0.1"; } }

		public override Guid[] Dependencies { get { return new Guid[] { _CurvesId, _ColorGradientId, _PulseId }; } }

		public override ParameterSignature Parameters
		{
			get
			{
				return new ParameterSignature(
					new ParameterSpecification("Color Handling", typeof(ChaseColorHandling)),
					new ParameterSpecification("Pulse Overlap", typeof(int)),
					new ParameterSpecification("Default channel level", typeof(Level)),
					new ParameterSpecification("Static Color", typeof(Color)),
					new ParameterSpecification("Color Gradient", typeof(ColorGradient)),
					new ParameterSpecification("Individual Pulse Curve", typeof(Curve)),
					new ParameterSpecification("Chase Movement Curve", typeof(Curve))
					);
			}
		}
	}
}