﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Threading;
using Vixen.Attributes;
using Vixen.Marks;
using Vixen.Module;
using Vixen.Module.Media;
using Vixen.Sys;
using Vixen.Sys.Attribute;
using Vixen.TypeConverters;
using VixenModules.App.ColorGradients;
using VixenModules.App.Curves;
using VixenModules.Effect.Effect;
using VixenModules.Effect.Pulse;
using VixenModules.EffectEditor.EffectDescriptorAttributes;
using VixenModules.Property.Location;
using ZedGraph;
using VixenModules.Media.Audio;

namespace VixenModules.Effect.Wipe
{
	public class WipeModule : BaseEffect
	{
		private WipeData _data;
		private EffectIntents _elementData = null;
		private readonly int _timeInterval = 50;
		private int _maxX;
		private int _minX;
		private int _maxY;
		private int _minY;
		private int _midX;
		private int _midY;
		private int _bufferWidth;
		private int _bufferHeight;
		private int _pulsePercent;
		private int _steps;
		private readonly AudioUtilities _audioUtilities;
		private const int Spacing = 50;
		private List<WipeAudioClass> _audioWipes;
		private IEnumerable<IMark> _marks = null;

		public WipeModule()
		{
			_data = new WipeData();
			_audioUtilities = new AudioUtilities();
			UpdateAttributes();
		}

		[Browsable(false)]
		private AudioUtilities AudioUtilities { get { return _audioUtilities; } }

		protected override void TargetNodesChanged()
		{
			CheckForInvalidColorData();
		}

		protected override void _PreRender(CancellationTokenSource tokenSource = null)
		{
			_elementData = new EffectIntents();
			
			List<IElementNode[]> renderNodes = new List<IElementNode[]>();
			List<Tuple<IElementNode, int, int, int>> renderedNodes = TargetNodes.SelectMany(x => x.GetLeafEnumerator())
				.Select(s =>
				{
					var prop = s.Properties.Get(LocationDescriptor._typeId);
					if (prop != null)
					{
						return new Tuple<IElementNode, int, int, int>(s, ((LocationData)prop.ModuleData).X,
							((LocationData)prop.ModuleData).Y, ((LocationData)prop.ModuleData).Z);
					}
					return new Tuple<IElementNode, int, int, int>(null, -1, -1, -1);
				})
				.Where(s => s.Item2 > 0)
				.ToList();

			if (!renderedNodes.Any()) return;

			_maxX = renderedNodes.Max(m => m.Item2);
			_maxY = renderedNodes.Max(m => m.Item3);
			_minX = renderedNodes.Min(m => m.Item2);
			_minY = renderedNodes.Min(m => m.Item3);
			_bufferWidth = _maxX - _minX;
			_bufferHeight = _maxY - _minY;
			_midX = _bufferWidth / 2;
			_midY = _bufferHeight / 2;

			switch (Direction)
			{
				case WipeDirection.DiagonalUp:
				case WipeDirection.DiagonalDown:
					renderNodes = GetRenderedDiagonal(renderedNodes);
					break;
				case WipeDirection.Vertical:
				case WipeDirection.Horizontal:
					renderNodes = GetRenderedLRUD(renderedNodes);
					break;
				case WipeDirection.Circle:
					renderNodes = GetRenderedCircle(renderedNodes);
					break;
				case WipeDirection.Dimaond:
					renderNodes = GetRenderedDiamond(renderedNodes);
					break;
				case WipeDirection.Burst:
					renderNodes = GetRenderedRectangle(renderedNodes);
					break;
			}

			switch (WipeMovement)
			{
				case WipeMovement.Count:
					RenderCount(renderNodes, tokenSource);
					break;
				case WipeMovement.PulseLength:
					RenderPulseLength(renderNodes, tokenSource);
					break;
				case WipeMovement.Movement:
				case WipeMovement.Audio:
				case WipeMovement.MarkCollection:
					RenderMovement(renderNodes, tokenSource);
					break;
			}
		}

		private List<IElementNode[]> GetRenderedCircle(List<Tuple<IElementNode, int, int, int>> renderedNodes)
		{
			List<Tuple<int, IElementNode[]>> groups = new List<Tuple<int, IElementNode[]>>();
			_steps = (int) (DistanceFromPoint(new Point(_maxX, _maxY), new Point(_minX, _minY)) / 2);
			
			Point centerPoint = new Point((int)((int)((XOffset + 100) / 2) * _bufferWidth / 100) + _minX, (int)((100 - (int)((YOffset + 100) / 2)) * _bufferHeight / 100) + _minY);
			int steps = GetMaxSteps(centerPoint);

			_pulsePercent = (int) (_bufferWidth * (PulsePercent / 100));
			if (WipeMovement >= WipeMovement.Movement) steps += _pulsePercent;

			for (int i = 0; i <= steps; i++)
			{
				List<IElementNode> elements = new List<IElementNode>();
				foreach (Tuple<IElementNode, int, int, int> node in renderedNodes)
				{
					int nodeLocation = (int)DistanceFromPoint(centerPoint, new Point(node.Item2, node.Item3));
					if (nodeLocation == i) elements.Add(node.Item1);
				}
				groups.Add(new Tuple<int, IElementNode[]>(i, elements.ToArray()));

			}
			return !ReverseDirection || WipeMovement >= WipeMovement.Movement
				? groups.OrderBy(o => o.Item1).Select(s => s.Item2).ToList()
				: groups.OrderByDescending(o => o.Item1).Select(s => s.Item2).ToList();
		}

		private List<IElementNode[]> GetRenderedDiamond(List<Tuple<IElementNode, int, int, int>> renderedNodes)
		{
			List<Tuple<int, IElementNode[]>> groups = new List<Tuple<int, IElementNode[]>>();
			_steps = (int)(Math.Sqrt(Math.Pow(_bufferWidth, 2) + Math.Pow(_bufferHeight, 2)) / 1.5);

			int xOffset = (int)((XOffset + 100) / 2);
			int yOffset = (int)((YOffset + 100) / 2);

			Point centerPoint = new Point((int)(xOffset * _bufferWidth / 100) + _minX, (int)((100 - yOffset) * _bufferHeight / 100) + _minY);
			int steps = (int)(GetMaxSteps(centerPoint) * 1.41);

			xOffset = (int)(Math.Round(ScaleCurveToValue(xOffset, -_bufferWidth, _bufferWidth)) / 2);
			yOffset = (int)(Math.Round(ScaleCurveToValue(yOffset, _bufferHeight, -_bufferHeight)) / 2);

			_pulsePercent = (int)(_bufferWidth * (PulsePercent / 100));
			if (WipeMovement >= WipeMovement.Movement) steps += _pulsePercent;
			
			for (int i = 0; i <= steps; i++)
			{
				List<IElementNode> elements = new List<IElementNode>();
				foreach (Tuple<IElementNode, int, int, int> node in renderedNodes)
				{
					// Do the Down/Left or Up/Right directions
					int nodeLocation = (int)((node.Item3 - _minY + yOffset) - (node.Item2 - _minX + xOffset) +
					                         (_bufferWidth - _bufferHeight) / 2);
					if (nodeLocation < 0) nodeLocation = -nodeLocation;
					if (nodeLocation == i &&
					    ((_maxY - _midY - yOffset - node.Item3) <= i && (_maxX - _midX - xOffset - node.Item2) <= i) &&
					    (node.Item3 - _minY - _midY + yOffset) <= i && (node.Item2 - _minX - _midX + xOffset) <= i)
					{
						elements.Add(node.Item1);
					}

					//Do the Down / Right or Up/ Left directions
					nodeLocation = (node.Item2 + xOffset - _minX + node.Item3 + yOffset - _minY) -
					               (_bufferWidth + _bufferHeight) / 2;
					if (nodeLocation < 0) nodeLocation = -nodeLocation;
					if (nodeLocation == i &&
					    ((_maxY - _midY - node.Item3 - yOffset) <= i && (_maxX - _midX - node.Item2 - xOffset) <= i) &&
					    (node.Item3 + yOffset - _minY - _midY) <= i && (node.Item2 + xOffset - _minX - _midX) <= i)
					{
						elements.Add(node.Item1);
					}
				}
				groups.Add(new Tuple<int, IElementNode[]>(i, elements.ToArray()));
			}
			return !ReverseDirection || WipeMovement >= WipeMovement.Movement
				? groups.OrderBy(o => o.Item1).Select(s => s.Item2).ToList()
				: groups.OrderByDescending(o => o.Item1).Select(s => s.Item2).ToList();

		}

		private List<IElementNode[]> GetRenderedRectangle(List<Tuple<IElementNode, int, int, int>> renderedNodes)
		{
			List<Tuple<int, IElementNode[]>> groups = new List<Tuple<int, IElementNode[]>>();
			_steps = (int)(Math.Max(_bufferWidth, _bufferHeight) / 2);

			int xOffset = (int)((XOffset + 100) / 2);
			int yOffset = (int)((YOffset + 100) / 2);

			Point centerPoint = new Point((int)(xOffset * _bufferWidth / 100) + _minX, (int)(yOffset * _bufferHeight / 100) + _minY);
			int steps = GetMaxSteps(centerPoint);

			xOffset = (int)Math.Round(ScaleCurveToValue(xOffset, -_bufferWidth, _bufferWidth)) / 2;
			yOffset = (int)Math.Round(ScaleCurveToValue(yOffset, _bufferHeight, -_bufferHeight)) / 2;

			_pulsePercent = (int)(_bufferWidth * (PulsePercent / 100));
			if (WipeMovement >= WipeMovement.Movement) steps += _pulsePercent;

			for (int i = 0; i <= steps; i++)
			{
				List<IElementNode> elements = new List<IElementNode>();

				foreach (Tuple<IElementNode, int, int, int> node in renderedNodes)
				{
					// Sets Left and Right side of burst
					if (_maxY - _midY - node.Item3 - yOffset <= i && _maxY - _midY - node.Item3 - yOffset >= -i &&
					    (_maxX - _midX - node.Item2 - xOffset == i || _maxX - _midX - node.Item2 - xOffset == -i))
						elements.Add(node.Item1);

					// Sets Top and Bottom side of burst
					if (_maxX - _midX - node.Item2 - xOffset <= i && _maxX - _midX - node.Item2 - xOffset >= -i &&
					    (_maxY - _midY - node.Item3 - yOffset == i || _maxY - _midY - node.Item3 - yOffset == -i))
						elements.Add(node.Item1);
				}
				groups.Add(new Tuple<int, IElementNode[]>(i, elements.ToArray()));
			}

			return !ReverseDirection || WipeMovement >= WipeMovement.Movement
				? groups.OrderBy(o => o.Item1).Select(s => s.Item2).ToList()
				: groups.OrderByDescending(o => o.Item1).Select(s => s.Item2).ToList();
		}

		private List<IElementNode[]> GetRenderedDiagonal(List<Tuple<IElementNode, int, int, int>> renderedNodes)
		{
			List<Tuple<int, IElementNode[]>> groups = new List<Tuple<int, IElementNode[]>>();
			_steps = (int)(Math.Sqrt(Math.Pow(_bufferWidth, 2) + Math.Pow(_bufferHeight, 2))*1.41);
			_pulsePercent = (int)(_bufferWidth * (PulsePercent / 100));
			if (WipeMovement >= WipeMovement.Movement) _steps += _pulsePercent;

			for (int i = 0; i <= _steps; i++)
			{
				List<IElementNode> elements = new List<IElementNode>();
				foreach (Tuple<IElementNode, int, int, int> node in renderedNodes)
				{
					if (ReverseDirection || WipeMovement >= WipeMovement.Movement)
					{
						if (Direction == WipeDirection.DiagonalUp)
						{
							if (node.Item2 - _minX + node.Item3 - _minY == _steps - i) elements.Add(node.Item1);
						}
						else
						{
							if ((node.Item3 - _minY) - (node.Item2 - _minX) + _bufferWidth == i) elements.Add(node.Item1);
						}
					}
					else
					{
						if (Direction == WipeDirection.DiagonalUp)
						{
							if ((node.Item2 - _minX) - (node.Item3 - _minY) + _bufferHeight ==
							    i)
								elements.Add(node.Item1);
						}
						else
						{
							if ((node.Item2 - _minX) + (node.Item3 - _minY) == i)
								elements.Add(node.Item1);
						}
					}
				}
				groups.Add(new Tuple<int, IElementNode[]>(i, elements.ToArray()));
			}
			return groups.OrderBy(o => o.Item1).Select(s => s.Item2).ToList();
		}

		private List<IElementNode[]> GetRenderedLRUD(List<Tuple<IElementNode, int, int, int>> renderedNodes)
		{
			List<Tuple<int, IElementNode[]>> groups = new List<Tuple<int, IElementNode[]>>();
			_steps = 0;

			_pulsePercent = Direction == WipeDirection.Vertical
				? (int)(_bufferHeight * (PulsePercent / 100))
				: (int)(_bufferWidth * (PulsePercent / 100));

			switch (Direction)
			{
				case WipeDirection.Vertical:
					_steps = _bufferHeight;
					break;
				case WipeDirection.Horizontal:
					_steps = _bufferWidth;
					break;
			}
			
			if (WipeMovement >= WipeMovement.Movement) _steps += _pulsePercent;
			
			for (int i = 0; i <= _steps; i++)
			{
				List<IElementNode> elements = new List<IElementNode>();
				switch (Direction)
				{
					case WipeDirection.Vertical:
						foreach (Tuple<IElementNode, int, int, int> node in renderedNodes)
						{
							if (_bufferHeight - (node.Item3 - _minY) == i) elements.Add(node.Item1);
						}

						break;

					case WipeDirection.Horizontal:
						foreach (Tuple<IElementNode, int, int, int> node in renderedNodes)
						{
							if (_bufferWidth - (node.Item2 - _minX) == i) elements.Add(node.Item1);
						}

						break;
				}
				groups.Add(new Tuple<int, IElementNode[]>(i, elements.ToArray()));
			}

			return ReverseDirection || WipeMovement >= WipeMovement.Movement
				? groups.OrderBy(o => o.Item1).Select(s => s.Item2).ToList()
				: groups.OrderByDescending(o => o.Item1).Select(s => s.Item2).ToList();
		}

		private void RenderPulseLength(List<IElementNode[]> renderNodes, CancellationTokenSource tokenSource)
		{
			TimeSpan effectTime = TimeSpan.Zero;
			double intervals = (double)PulseTime / renderNodes.Count();
			var intervalTime = TimeSpan.FromMilliseconds(intervals);
			// the calculation above blows up render time/memory as count goes up, try this.. 
			// also fails if intervals is less than half a ms and intervalTime then gets 0
			intervalTime = TimeSpan.FromMilliseconds(Math.Max(intervalTime.TotalMilliseconds, 5));
			TimeSpan segmentPulse = TimeSpan.FromMilliseconds(PulseTime);
			while (effectTime < TimeSpan)
			{
				foreach (var item in renderNodes)
				{
					if (tokenSource != null && tokenSource.IsCancellationRequested)
						return;
					foreach (IElementNode element in item)
					{
						if (tokenSource != null && tokenSource.IsCancellationRequested)
							return;
						if (element == null) continue;

						EffectIntents result;
						if (ColorHandling == ColorHandling.GradientThroughWholeEffect)
						{
							result = PulseRenderer.RenderNode(element, _data.Curve, _data.ColorGradient, segmentPulse,
								HasDiscreteColors);
							result.OffsetAllCommandsByTime(effectTime);

							_elementData.Add(result);
						}
						else
						{
							double positionWithinGroup = (double)(1.0 / (TimeSpan.Ticks - segmentPulse.Ticks)) * (effectTime.Ticks);
							if (ColorAcrossItemPerCount) positionWithinGroup = positionWithinGroup * PassCount % 1;
							if (HasDiscreteColors)
							{
								List<Tuple<Color, float>> colorsAtPosition =
									ColorGradient.GetDiscreteColorsAndProportionsAt(positionWithinGroup);
								foreach (Tuple<Color, float> colorProportion in colorsAtPosition)
								{
									float proportion = colorProportion.Item2;
									// scale all levels of the pulse curve by the proportion that is applicable to this color
									Curve newCurve = new Curve(Curve.Points);
									foreach (PointPair pointPair in newCurve.Points)
									{
										pointPair.Y *= proportion;
									}

									result = PulseRenderer.RenderNode(element, newCurve,
										new ColorGradient(colorProportion.Item1), segmentPulse, HasDiscreteColors);
									result.OffsetAllCommandsByTime(effectTime);
									
								}
							}
							else
							{
								result = PulseRenderer.RenderNode(element, _data.Curve,
									new ColorGradient(_data.ColorGradient.GetColorAt(positionWithinGroup)),
									segmentPulse, HasDiscreteColors);
								result.OffsetAllCommandsByTime(effectTime);
								
								_elementData.Add(result);
							}
						}
					}
					effectTime += intervalTime;
					if (effectTime >= TimeSpan)
						return;
				}
			}

		}

		private void RenderCount(List<IElementNode[]> renderNodes, CancellationTokenSource tokenSource)
		{
			TimeSpan effectTime = TimeSpan.Zero;
			int count = 0;
			double pulseSegment = (TimeSpan.Ticks * ((PulsePercent * ((double)_steps / renderNodes.Count())) / 100)) / PassCount;
			TimeSpan intervalTime = TimeSpan.FromTicks((long)((TimeSpan.Ticks - pulseSegment) / (renderNodes.Count() * PassCount)));
			TimeSpan segmentPulse = TimeSpan.FromTicks((long)pulseSegment);

			while (count < PassCount)
			{
				foreach (IElementNode[] item in renderNodes)
				{
					if (tokenSource != null && tokenSource.IsCancellationRequested) return;

					foreach (IElementNode element in item)
					{
						if (tokenSource != null && tokenSource.IsCancellationRequested)
							return;
						if (element == null) continue;

						EffectIntents result;
						if (ColorHandling == ColorHandling.GradientThroughWholeEffect)
						{
							result = PulseRenderer.RenderNode(element, _data.Curve, _data.ColorGradient, segmentPulse,
								HasDiscreteColors);
							result.OffsetAllCommandsByTime(effectTime);
							if (WipeOff && count == 0 && result.Any())
							{
								foreach (var effectIntent in result.FirstOrDefault().Value)
								{
									_elementData.Add(PulseRenderer.GenerateStartingStaticPulse(element, effectIntent,
										HasDiscreteColors));
								}
							}

							if (WipeOn && result.Any() && count == PassCount - 1)
							{
								foreach (var effectIntent in result.FirstOrDefault().Value)
								{
									_elementData.Add(PulseRenderer.GenerateExtendedStaticPulse(element, effectIntent,
										TimeSpan, HasDiscreteColors));
								}
							}

							_elementData.Add(result);
						}
						else
						{
							double positionWithinGroup = (double)(1.0 / (TimeSpan.Ticks - segmentPulse.Ticks)) * (effectTime.Ticks);
							if (ColorAcrossItemPerCount) positionWithinGroup = positionWithinGroup * PassCount % 1;
							if (HasDiscreteColors)
							{
								List<Tuple<Color, float>> colorsAtPosition =
									ColorGradient.GetDiscreteColorsAndProportionsAt(positionWithinGroup);
								foreach (Tuple<Color, float> colorProportion in colorsAtPosition)
								{
									float proportion = colorProportion.Item2;
									// scale all levels of the pulse curve by the proportion that is applicable to this color
									Curve newCurve = new Curve(Curve.Points);
									foreach (PointPair pointPair in newCurve.Points)
									{
										pointPair.Y *= proportion;
									}

									result = PulseRenderer.RenderNode(element, newCurve,
										new ColorGradient(colorProportion.Item1), segmentPulse, HasDiscreteColors);
									result.OffsetAllCommandsByTime(effectTime);

									if (WipeOff && count == 0)
									{
										foreach (var effectIntent in result.FirstOrDefault().Value)
										{
											_elementData.Add(PulseRenderer.GenerateStartingStaticPulse(element,
												effectIntent, HasDiscreteColors,
												new ColorGradient(colorProportion.Item1)));
										}
									}

									if (result.Count > 0) _elementData.Add(result);

									if (WipeOn && count == PassCount - 1)
									{
										foreach (var effectIntent in result.FirstOrDefault().Value)
										{
											_elementData.Add(PulseRenderer.GenerateExtendedStaticPulse(element,
												effectIntent, TimeSpan, HasDiscreteColors,
												new ColorGradient(colorProportion.Item1)));
										}
									}
								}
							}
							else
							{
								result = PulseRenderer.RenderNode(element, _data.Curve,
									new ColorGradient(_data.ColorGradient.GetColorAt(positionWithinGroup)),
									segmentPulse, HasDiscreteColors);
								result.OffsetAllCommandsByTime(effectTime);

								if (WipeOff && count == 0)
								{
									foreach (var effectIntent in result.FirstOrDefault().Value)
									{
										_elementData.Add(PulseRenderer.GenerateStartingStaticPulse(element,
											effectIntent, HasDiscreteColors));
									}
								}

								if (WipeOn && count == PassCount - 1)
								{
									foreach (var effectIntent in result.FirstOrDefault().Value)
									{
										_elementData.Add(PulseRenderer.GenerateExtendedStaticPulse(element,
											effectIntent, TimeSpan, HasDiscreteColors));
									}
								}
								_elementData.Add(result);
							}
						}
					}
					effectTime += intervalTime;
				}
				count++;
			}
		}
		
		private void RenderMovement(List<IElementNode[]> renderNodes, CancellationTokenSource tokenSource)
		{
			double previousMovement = 2.0;
			TimeSpan startTime = TimeSpan.Zero;
			double startLabel = 0;
			double endLabel = 0;
			TimeSpan startTime1 = StartTime;
			TimeSpan endTime1 = StartTime;
			TimeSpan timeInterval = TimeSpan.FromMilliseconds(_timeInterval);
			int intervals = Convert.ToInt32(Math.Ceiling(TimeSpan.TotalMilliseconds / _timeInterval));
			int burst = Direction != WipeDirection.DiagonalUp ? 0 : _pulsePercent - 1;

			if (WipeMovement == WipeMovement.Audio)
			{
				GetAudioSettings();
				UpdateAudioAttributes();
			}

			List<WipeClass> renderElements = new List<WipeClass>();
			for (int i = 0; i < intervals; i++)
			{
				double position = (double) 100 / intervals * i;
				
				double movement = 0;
				switch (WipeMovement)
				{
					case WipeMovement.Audio:
					{
						var currentValue = _audioUtilities.VolumeAtTime(i * _timeInterval);
						if (currentValue > ((double) Sensitivity / 10))
						{
							movement = ReverseDirection
								? 1 + ScaleCurveToValue(currentValue, 1, 0) * 10
								: -ScaleCurveToValue(currentValue, 1, 0) * 10;
						}

						break;
					}
					case WipeMovement.MarkCollection:
					{
						SetupMarks();
						if (_marks != null)
						{
							foreach (var mark in _marks)
							{
								if (StartTime + TimeSpan.FromMilliseconds(i * _timeInterval) < mark.StartTime)
								{
									endTime1 = mark.StartTime;
									double.TryParse(mark.Text, out endLabel);
									break;
								}

								startTime1 = mark.StartTime;
								double.TryParse(mark.Text, out startLabel);
								}

							movement = (i * _timeInterval - (startTime1 - StartTime).TotalMilliseconds) * ((endLabel - startLabel) / (endTime1 - startTime1).TotalMilliseconds) /
							           100 + startLabel / 100;
						}

						break;
					}
					default:
						movement = MovementCurve.GetValue(position) / 100;
						break;
				}

				if (previousMovement != movement)
				{
					if (renderElements.Count > 0)
						renderElements.Last().Duration = startTime - renderElements.Last().StartTime;

					WipeClass wc = new WipeClass
					{
						ElementIndex = (int) ((renderNodes.Count - 1) * movement),
						StartTime = startTime,
						Duration = TimeSpan - startTime
					};

					if (ReverseColorDirection)
					{
						wc.ReverseColorDirection = previousMovement < movement ? 0 : 1;
					}

					renderElements.Add(wc);
				}

				previousMovement = movement;
				startTime += timeInterval;
			}

			double pos = ((double)100 / _pulsePercent) / 100;
			// Now render element
			foreach (var wipeNode in renderElements)
			{
				for (int i = 0; i < _pulsePercent; i++)
				{
					double position = wipeNode.ReverseColorDirection - pos * i;
					if (position < 0) position = -position;
					Color color = _data.ColorGradient.GetColorAt(position);
					double curveValue = _data.Curve.GetValue(position * 100) / 100;

					if (wipeNode.ElementIndex - i > 0 && wipeNode.ElementIndex - i + burst < renderNodes.Count)
					{
						IElementNode[] elementGroup = renderNodes[wipeNode.ElementIndex - i + burst];
						if (tokenSource != null && tokenSource.IsCancellationRequested) return;

						foreach (var item in elementGroup)
						{
							if (tokenSource != null && tokenSource.IsCancellationRequested)
								return;
							if (item != null)
							{
								var result = PulseRenderer.RenderNode(item, curveValue, color, wipeNode.Duration);
								result.OffsetAllCommandsByTime(wipeNode.StartTime);
								_elementData.Add(result);
							}
						}
					}
				}
			}
		}

		private int GetMaxSteps(Point centerPoint)
		{
			//Determine max distance from center point.
			int steps = (int)(DistanceFromPoint(new Point(_maxX, _maxY), centerPoint));
			steps = Math.Max((int)(DistanceFromPoint(new Point(_maxX, _minY), centerPoint)), steps);
			steps = Math.Max((int)(DistanceFromPoint(new Point(_minX, _minY), centerPoint)), steps);
			return Math.Max((int)(DistanceFromPoint(new Point(_minX, _maxY), centerPoint)), steps);
		}

		private void GetAudioSettings()
		{
			if (Media != null)
				foreach (IMediaModuleInstance module in Media)
				{
					var audio = module as Audio;
					if (audio != null)
					{
						if (audio.Channels == 0)
						{
							continue;
						}
						_audioUtilities.TimeSpan = TimeSpan;
						_audioUtilities.StartTime = StartTime;
						_audioUtilities.ReloadAudio(audio);
					}
				}
		}

		private void SetupMarks()
		{
			IMarkCollection mc = MarkCollections.FirstOrDefault(x => x.Id == _data.MarkCollectionId);
			_marks = mc?.MarksInclusiveOfTime(StartTime, StartTime + TimeSpan);
		}

		/// <inheritdoc />
		protected override void MarkCollectionsChanged()
		{
			if (WipeMovement == WipeMovement.MarkCollection)
			{
				var markCollection = MarkCollections.FirstOrDefault(x => x.Name.Equals(MarkCollectionId));
				InitializeMarkCollectionListeners(markCollection);
			}
		}

		/// <inheritdoc />
		protected override void MarkCollectionsRemoved(IList<IMarkCollection> addedCollections)
		{
			var mc = addedCollections.FirstOrDefault(x => x.Id == _data.MarkCollectionId);
			if (mc != null)
			{
				//Our collection is gone!!!!
				RemoveMarkCollectionListeners(mc);
				MarkCollectionId = String.Empty;
			}
		}

		private class WipeClass
		{
			public int ElementIndex;
			public TimeSpan StartTime;
			public TimeSpan Duration;
			public int ReverseColorDirection;
		}

		public class WipeAudioClass
		{
			public int X;
			public int Y;
		}

		protected override EffectIntents _Render()
		{
			return _elementData;
		}

		public override IModuleDataModel ModuleData
		{
			get { return _data; }
			set
			{
				_data = value as WipeData;
				CheckForInvalidColorData();
				IsDirty = true;
				UpdateAttributes();
			}
		}

		protected override EffectTypeModuleData EffectModuleData
		{
			get { return _data; }
		}

		private void CheckForInvalidColorData()
		{
			var validColors = GetValidColors();
			if (validColors.Any())
			{
				if (!_data.ColorGradient.GetColorsInGradient().IsSubsetOf(validColors))
				{
					//Our color is not valid for any elements we have.
					//Try to set a default color gradient from our available colors
					_data.ColorGradient = new ColorGradient(validColors.First());
				}
			}
		}

		[Value]
		[ProviderCategory(@"Color", 3)]
		[ProviderDisplayName(@"ColorHandling")]
		[ProviderDescription(@"ColorHandling")]
		[PropertyOrder(0)]
		public ColorHandling ColorHandling
		{
			get { return _data.ColorHandling; }
			set
			{
				_data.ColorHandling = value;
				IsDirty = true;
				OnPropertyChanged();
				UpdateAttributes();
				TypeDescriptor.Refresh(this);
			}
		}

		[Value]
		[ProviderCategory(@"Color", 3)]
		[ProviderDisplayName(@"ColorGradient")]
		[ProviderDescription(@"Color")]
		[PropertyOrder(1)]
		public ColorGradient ColorGradient
		{
			get
			{
				return _data.ColorGradient;
			}
			set
			{
				_data.ColorGradient = value;
				IsDirty = true;
				OnPropertyChanged();
			}
		}

		[Value]
		[ProviderCategory(@"Color", 3)]
		[ProviderDisplayName(@"ColorPerCount")]
		[ProviderDescription(@"ColorPerCount")]
		[PropertyOrder(2)]
		public bool ColorAcrossItemPerCount
		{
			get { return _data.ColorAcrossItemPerCount; }
			set
			{
				_data.ColorAcrossItemPerCount = value;
				IsDirty = true;
				OnPropertyChanged();
			}
		}

		[Value]
		[ProviderCategory(@"Color", 3)]
		[ProviderDisplayName(@"ReverseColorCurve")]
		[ProviderDescription(@"ReverseColorCurve")]
		[PropertyOrder(3)]
		public bool ReverseColorDirection
		{
			get { return _data.ReverseColorDirection; }
			set
			{
				_data.ReverseColorDirection = value;
				IsDirty = true;
				OnPropertyChanged();
			}
		}

		[Value]
		[ProviderCategory(@"Direction",2)]
		[DisplayName(@"Direction")]
		[ProviderDescription(@"Direction")]
		[PropertyOrder(0)]
		public WipeDirection Direction
		{
			get { return _data.Direction; }
			set
			{
				_data.Direction = value;
				IsDirty = true;
				OnPropertyChanged();
				UpdateAttributes();
				TypeDescriptor.Refresh(this);

			}
		}

		[Value]
		[ProviderCategory(@"Direction", 2)]
		[ProviderDisplayName(@"ReverseDirection")]
		[ProviderDescription(@"ReverseDirection")]
		[PropertyOrder(1)]
		public bool ReverseDirection
		{
			get { return _data.ReverseDirection; }
			set
			{
				_data.ReverseDirection = value;
				IsDirty = true;
				OnPropertyChanged();
				UpdateAttributes();
				TypeDescriptor.Refresh(this);

			}
		}

		[Value]
		[ProviderCategory(@"Direction", 2)]
		[ProviderDisplayName(@"Movement")]
		[ProviderDescription(@"Movement")]
		[PropertyOrder(2)]
		public Curve MovementCurve
		{
			get { return _data.MovementCurve; }
			set
			{
				_data.MovementCurve = value;
				IsDirty = true;
				OnPropertyChanged();
				UpdateAttributes();
				TypeDescriptor.Refresh(this);

			}
		}

		[Value]
		[ProviderCategory(@"Movement", 3)]
		[ProviderDisplayName(@"XOffset")]
		[ProviderDescription(@"XOffset")]
		[PropertyEditor("SliderDoubleEditor")]
		[NumberRange(-100, 100, 1)]
		[PropertyOrder(3)]
		public double XOffset
		{
			get { return _data.XOffset; }
			set
			{
				_data.XOffset = value;
				IsDirty = true;
				OnPropertyChanged();
			}
		}

		[Value]
		[ProviderCategory(@"Movement", 3)]
		[ProviderDisplayName(@"YOffset")]
		[ProviderDescription(@"YOffset")]
		[PropertyEditor("SliderDoubleEditor")]
		[NumberRange(-100, 100, 1)]
		[PropertyOrder(3)]
		public double YOffset
		{
			get { return _data.YOffset; }
			set
			{
				_data.YOffset = value;
				IsDirty = true;
				OnPropertyChanged();
			}
		}

		[Value]
		[ProviderCategory(@"Brightness",4)]
		[ProviderDisplayName(@"Brightness")]
		[ProviderDescription(@"PulseShape")]
		public Curve Curve
		{
			get { return _data.Curve; }
			set
			{
				_data.Curve = value;
				IsDirty = true;
				OnPropertyChanged();
			}
		}

		[Value]
		[ProviderCategory(@"Pulse",6)]
		[ProviderDisplayName(@"PulseDuration")]
		[ProviderDescription(@"PulseDuration")]
		public int PulseTime
		{
			get { return _data.PulseTime; }
			set
			{
				_data.PulseTime = value;
				IsDirty = true;
				OnPropertyChanged();
			}
		}

		[Value]
		[ProviderCategory(@"Type", 1)]
		[ProviderDisplayName(@"Movement")]
		[ProviderDescription(@"Movement")]
		[PropertyOrder(0)]
		public WipeMovement WipeMovement
		{
			get { return _data.WipeMovement; }
			set
			{
				_data.WipeMovement = value;
				IsDirty = true;
				OnPropertyChanged();
				UpdateAttributes();
				UpdateAudioAttributes();
				TypeDescriptor.Refresh(this);

			}
		}

		[Value]
		[ProviderCategory(@"Type", 1)]
		[ProviderDisplayName(@"Mark Collection")]
		[ProviderDescription(@"Mark Collection that has the explosions align to.")]
		[TypeConverter(typeof(IMarkCollectionNameConverter))]
		[PropertyEditor("SelectionEditor")]
		[PropertyOrder(2)]
		public string MarkCollectionId
		{
			get
			{
				return MarkCollections.FirstOrDefault(x => x.Id == _data.MarkCollectionId)?.Name;
			}
			set
			{
				var newMarkCollection = MarkCollections.FirstOrDefault(x => x.Name.Equals(value));
				var id = newMarkCollection?.Id ?? Guid.Empty;
				if (!id.Equals(_data.MarkCollectionId))
				{
					var oldMarkCollection = MarkCollections.FirstOrDefault(x => x.Id.Equals(_data.MarkCollectionId));
					RemoveMarkCollectionListeners(oldMarkCollection);
					_data.MarkCollectionId = id;
					AddMarkCollectionListeners(newMarkCollection);
					IsDirty = true;
					OnPropertyChanged();
				}
			}
		}

		[Value]
		[ProviderCategory(@"Type",1)]
		[ProviderDisplayName(@"WipeCount")]
		[ProviderDescription(@"WipeCount")]
		[PropertyOrder(1)]
		public int PassCount
		{
			get { return _data.PassCount; }
			set
			{
				_data.PassCount = value;
				IsDirty = true;
				OnPropertyChanged();
			}
		}

		[Value]
		[ProviderCategory(@"Type", 1)]
		[ProviderDisplayName(@"WipeOn")]
		[ProviderDescription(@"ExtendPulseEnd")]
		[PropertyOrder(2)]
		public bool WipeOn
		{
			get { return _data.WipeOn; }
			set
			{
				_data.WipeOn = value;
				IsDirty = true;
				OnPropertyChanged();
			}
		}

		[Value]
		[ProviderCategory(@"Type", 1)]
		[ProviderDisplayName(@"WipeOff")]
		[ProviderDescription(@"ExtendPulseStart")]
		[PropertyOrder(3)]
		public bool WipeOff
		{
			get { return _data.WipeOff; }
			set
			{
				_data.WipeOff = value;
				IsDirty = true;
				OnPropertyChanged();
			}
		}

		#region Audio

		[Value]
		[ProviderCategory(@"Audio", 2)]
		[ProviderDisplayName(@"Volume Sensitivity")]
		[ProviderDescription(@"The range of the volume levels displayed by the effect")]
		[PropertyEditor("SliderEditor")]
		[NumberRange(-200, 0, 1)]
		[PropertyOrder(0)]
		public int Sensitivity
		{
			get { return _data.Sensitivity; }
			set
			{
				_data.Sensitivity = value;
				IsDirty = true;
				OnPropertyChanged();
			}
		}

		[Value]
		[ProviderCategory(@"Audio", 2)]
		[ProviderDisplayName(@"Gain")]
		[ProviderDescription(@"Boosts the volume")]
		[PropertyEditor("SliderEditor")]
		[NumberRange(0, 200, .5)]
		[PropertyOrder(1)]
		public int Gain
		{
			get { return _data.Gain * 10; }
			set
			{
				_data.Gain = value / 10;
				_audioUtilities.Gain = value / 10;
				IsDirty = true;
				OnPropertyChanged();
			}
		}

		[Value]
		[ProviderCategory(@"Audio", 2)]
		[ProviderDisplayName(@"HighPassFilter")]
		[ProviderDescription(@"Ignores frequencies below a given frequency")]
		[PropertyOrder(2)]
		public bool HighPass
		{
			get { return _data.HighPass; }
			set
			{
				_data.HighPass = value;
				_audioUtilities.HighPass = value;
				IsDirty = true;
				UpdateLowHighPassAttributes();
				OnPropertyChanged();
			}
		}

		[Value]
		[ProviderCategory(@"Audio", 2)]
		[ProviderDisplayName(@"HighPassFrequency")]
		[ProviderDescription(@"Ignore frequencies below this value")]
		[PropertyOrder(3)]
		public int HighPassFreq
		{
			get { return _data.HighPassFreq; }
			set
			{
				_data.HighPassFreq = value;
				_audioUtilities.HighPassFreq = value;
				IsDirty = true;
				OnPropertyChanged();
			}
		}

		[Value]
		[ProviderCategory(@"Audio", 2)]
		[ProviderDisplayName(@"LowPassFilter")]
		[ProviderDescription(@"Ignores frequencies above a given frequency")]
		[PropertyOrder(4)]
		public bool LowPass
		{
			get { return _data.LowPass; }
			set
			{
				_data.LowPass = value;
				_audioUtilities.LowPass = value;
				IsDirty = true;
				UpdateLowHighPassAttributes();
				OnPropertyChanged();
			}
		}

		[Value]
		[ProviderCategory(@"Audio", 2)]
		[ProviderDisplayName(@"LowPassFrequency")]
		[ProviderDescription(@"Ignore frequencies above this value")]
		[PropertyOrder(5)]
		public int LowPassFreq
		{
			get { return _data.LowPassFreq; }
			set
			{
				_data.LowPassFreq = value;
				_audioUtilities.LowPassFreq = value;
				IsDirty = true;
				OnPropertyChanged();
			}
		}


		[Value]
		[ProviderCategory(@"Audio", 2)]
		[ProviderDescription(@"Brings the peak volume of the selected audio range to the top of the effect")]
		[PropertyOrder(6)]
		public bool Normalize
		{
			get { return _data.Normalize; }
			set
			{
				_audioUtilities.Normalize = value;
				_data.Normalize = value;
				IsDirty = true;
				OnPropertyChanged();
			}
		}

		[Value]
		[ProviderCategory(@"Audio", 2)]
		[ProviderDisplayName(@"AttackTime")]
		[ProviderDescription(@"How quickly the effect initially reacts to a volume peak")]
		[PropertyEditor("SliderEditor")]
		[NumberRange(0, 300, 10)]
		[PropertyOrder(7)]
		public int AttackTime
		{
			get { return _data.AttackTime; }
			set
			{
				_data.AttackTime = value;
				_audioUtilities.AttackTime = value;
				IsDirty = true;
				OnPropertyChanged();
			}
		}

		[Value]
		[ProviderCategory(@"Audio", 2)]
		[ProviderDisplayName(@"DecayTime")]
		[ProviderDescription(@"How quickly the effect falls from a volume peak")]
		[PropertyEditor("SliderEditor")]
		[NumberRange(1, 5000, 300)]
		[PropertyOrder(8)]
		public int DecayTime
		{
			get { return _data.DecayTime; }
			set
			{
				_data.DecayTime = value;
				_audioUtilities.DecayTime = value;
				IsDirty = true;
				OnPropertyChanged();
			}
		}

		#endregion

		[Value]
		[ProviderCategory(@"Pulse",7)]
		[ProviderDisplayName(@"PulsePercent")]
		[ProviderDescription(@"WipePulsePercent")]
		[PropertyEditor("SliderDoubleEditor")]
		public double PulsePercent
		{
			get { return _data.PulsePercent; }
			set
			{
				_data.PulsePercent = value;
				IsDirty = true;
				OnPropertyChanged();
			}
		}

		#region Information

		public override string Information
		{
			get { return "Visit the Vixen Lights website for more information on this effect."; }
		}

		public override string InformationLink
		{
			get { return "http://www.vixenlights.com/vixen-3-documentation/sequencer/effects/wipe/"; }
		}

		#endregion

		#region Attributes

		private void UpdateAttributes()
		{
			Dictionary<string, bool> propertyStates = new Dictionary<string, bool>(14)
			{
				{"PassCount", WipeMovement == WipeMovement.Count},
				{"PulsePercent", WipeMovement != WipeMovement.PulseLength},
				{"WipeOn", WipeMovement == WipeMovement.Count},
				{"WipeOff", WipeMovement == WipeMovement.Count},
				{"PulseTime", WipeMovement == WipeMovement.PulseLength},
				{"ColorHandling", WipeMovement != WipeMovement.Movement},
				{"WipeMovementDirection", WipeMovement == WipeMovement.Movement },
				{"MovementCurve", WipeMovement == WipeMovement.Movement },
				{"ReverseDirection", WipeMovement != WipeMovement.Movement },
				{"ColorAcrossItemPerCount", ColorHandling == ColorHandling.ColorAcrossItems && WipeMovement != WipeMovement.Movement},
				{"ReverseColorDirection", WipeMovement == WipeMovement.Movement},
				{"YOffset", Direction > (WipeDirection) 3},
				{"XOffset",  Direction > (WipeDirection) 3},
				{"MarkCollectionId", WipeMovement == WipeMovement.MarkCollection}
		};
			SetBrowsable(propertyStates);
		}

		private void UpdateAudioAttributes(bool refresh = true)
		{
			Dictionary<string, bool> propertyStates = new Dictionary<string, bool>(10);
			propertyStates.Add("Sensitivity", WipeMovement == WipeMovement.Audio && _audioUtilities.AudioLoaded);
			propertyStates.Add("LowPass", WipeMovement == WipeMovement.Audio && _audioUtilities.AudioLoaded);
			propertyStates.Add("LowPassFreq", WipeMovement == WipeMovement.Audio && _audioUtilities.AudioLoaded);
			propertyStates.Add("HighPass", WipeMovement == WipeMovement.Audio && _audioUtilities.AudioLoaded);
			propertyStates.Add("HighPassFreq", WipeMovement == WipeMovement.Audio && _audioUtilities.AudioLoaded);
			propertyStates.Add("Range", WipeMovement == WipeMovement.Audio && _audioUtilities.AudioLoaded);
			propertyStates.Add("Normalize", WipeMovement == WipeMovement.Audio && _audioUtilities.AudioLoaded);
			propertyStates.Add("DecayTime", WipeMovement == WipeMovement.Audio && _audioUtilities.AudioLoaded);
			propertyStates.Add("AttackTime", WipeMovement == WipeMovement.Audio && _audioUtilities.AudioLoaded);
			propertyStates.Add("Gain", WipeMovement == WipeMovement.Audio && _audioUtilities.AudioLoaded);
			SetBrowsable(propertyStates);
			if (refresh)
			{
				TypeDescriptor.Refresh(this);
			}

			UpdateLowHighPassAttributes();
		}

		protected void UpdateLowHighPassAttributes(bool refresh = true)
		{
			Dictionary<string, bool> propertyStates = new Dictionary<string, bool>(2)
			{
				{"LowPassFreq", LowPass},
				{"HighPassFreq", HighPass}
			};
			SetBrowsable(propertyStates);
			if (refresh)
			{
				TypeDescriptor.Refresh(this);
			}
		}

		#endregion

	}
}