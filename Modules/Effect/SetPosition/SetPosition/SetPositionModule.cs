﻿using System;
using Vixen.Commands;
using Vixen.Commands.KnownDataTypes;
using Vixen.Module;
using Vixen.Module.Effect;
using Vixen.Sys;

namespace VixenModules.Effect.SetPosition {
	public class SetPositionModule : EffectModuleInstanceBase {
		private SetPositionData _data;
		private ChannelData _channelData;

		public override IModuleDataModel ModuleData {
			get { return _data; }
			set { _data = value as SetPositionData; }
		}

		public override object[] ParameterValues {
			get { return new object[] { _data.Position };}
			set {
				_data.Position = value[0].DynamicCast<Position>();
			}
		}

		public Position Position {
			get { return _data.Position; }
			set {
				_data.Position = value;
				IsDirty = true;
			}
		}

		protected override void _PreRender() {
			_channelData = new ChannelData();

			foreach(ChannelNode node in TargetNodes) {
				foreach(Channel channel in node.GetChannelEnumerator()) {
					Command setPositionCommand = new Animatronics.BasicPositioning.SetPosition(Position);
					CommandNode data = new CommandNode(setPositionCommand, TimeSpan.Zero, TimeSpan);
					if(channel != null)
						_channelData[channel.Id] = new[] { data };
				}
			}
		}

		protected override ChannelData _Render() {
			return _channelData;
		}
	}
}
