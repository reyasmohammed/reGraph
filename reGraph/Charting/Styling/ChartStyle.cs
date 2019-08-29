﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;

namespace AeoGraphing.Charting.Styling
{
  public class ChartStyle
  {
    public Measure Padding { get; set; }
    public string NumericFormat { get; set; }
    public Font TitleFont { get; set; }
    public Font DescriptionFont { get; set; }
    public Font AxisCaptionFont { get; set; }
    public Font DataCaptionFont { get; set; }
    public Measure DataCaptionPadding { get; set; }
    public Color TextColor { get; set; }
    public Color BackgroundColor { get; set; }

    public bool DrawTitle { get; set; }
    public bool DrawDescription { get; set; }
  }
}