﻿using AeoGraphing.Charting;
using reGraph.Data;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;

namespace AeoGraphing.Data.Query
{
  public class DataQuery
  {
    private Type _type;
    private IEnumerable<IDateable> _data;
    private static readonly string DATE_FORMAT = "dd.MM.yy HH:mm";

    private static readonly string[] SINCE_INDICATORS = new string[] { "since", "seit" };
    private static readonly string[] FROM_INDICATORS = new string[] { "from", "von" };
    private static readonly string[] TO_INDICATORS = new string[] { "to", "until", "bis", "-" };
    private static readonly string[] OPTION_INDICATORS = new string[] { "options", "optionen" };

    private static readonly Dictionary<string, string> METHOD_NAMES = new Dictionary<string, string>
    {
      { "avg", "Average of" },
      { "sum", "Sum of" },
      { "max", "Maximum of" },
      { "min", "Minimum of" },
      { "count", "Count of" },
      { "distinct", "Count of different" },
    };

    private static readonly Dictionary<string, Func<IEnumerable<decimal>, decimal>> METHODS = new Dictionary<string, Func<IEnumerable<decimal>, decimal>>
    {
      { "avg", (x) => x.Count() == 0 ? 0 : x.Average() },
      { "sum", (x) => x.Sum() },
      { "max", (x) => x.Count() == 0 ? 0 : x.Max() },
      { "min", (x) => x.Count() == 0 ? 0 : x.Min() },
      { "distinct", (x) => x.Distinct().Count() },
      { "count", (x) => x.Count() }
    };

    public DataQuery(IEnumerable<IDateable> dataCollection)
    {
      this._type = dataCollection.GetType().GetGenericArguments()[0];
      this._data = dataCollection;
    }

    private int indexOf(string str, string[] arr, out int len)
    {
      foreach (var cmp in arr)
      {
        len = cmp.Length;
        var index = str.IndexOf(cmp);
        if (index >= 0)
          return index;
      }

      len = 0;
      return -1;
    }

    public DataCollection Query(string query, string name, out Dictionary<string, string> options, IFormatProvider formatProvider = null)
    {
      formatProvider = formatProvider ?? CultureInfo.GetCultureInfo("de-CH");
      options = new Dictionary<string, string>();
      var split = query.Split('|');
      var series = split[0].SplitIgnore(',', '(', ')').Select(x => x.Trim()).ToArray();
      var period = split[1].Trim();
      var index = period.IndexOf(' ');
      TimeSpan timespan;
      if (TimeSpan.TryParse(period.Substring(0, index), out timespan) == false)
        return null;

      DateTime from = DateTime.MinValue;
      DateTime to = DateTime.MinValue;
      period = period.Substring(index + 1).Trim();
      index = period.IndexOf(' ');
      var indicator = period.Substring(0, index);
      period = period.Substring(index + 1).Trim();


      index = indexOf(period, OPTION_INDICATORS, out var olen);
      if (index >= 0)
      {
        string opt = period.Substring(index + olen);
        period = period.Substring(0, index);
        foreach (var option in opt.Split(','))
        {
          split = option.Split('=');
          options.Add(split[0].ToLower().Trim(), split[1].Trim());
        }
      }

      if (options.TryGetValue("dateformat", out var dateFormat) == false)
        dateFormat = DATE_FORMAT;

      dateFormat = dateFormat.Replace("\\:", ":");

      if (SINCE_INDICATORS.Contains(indicator))
      {
        from = DateTime.Parse(period, formatProvider);
        to = DateTime.UtcNow;
      }
      else if (FROM_INDICATORS.Contains(indicator))
      {
        index = indexOf(period, TO_INDICATORS, out var len);
        from = DateTime.Parse(period.Substring(0, index).Trim(), formatProvider);
        to = DateTime.Parse(period.Substring(index + len).Trim(), formatProvider);
      }

      var dseries = new List<DataSeries>();
      var data = _data.Where(x => x.DateTime >= from && x.DateTime <= to).ToList();
      from = data.Min(x => x.DateTime).RoundUp(timespan);
      to = data.Max(x => x.DateTime).RoundUp(timespan);

      var groupingInterval = TimeSpan.MinValue;
      List<string> dataGroupNames = null;
      List<double> dataGroupValues = null;

      foreach (var s in series)
      {
        double scale = 1;
        index = s.IndexOf('(');
        var func = s.Substring(0, index).ToLower();
        string path = name;

        var rest = s.Substring(index + 1);
        index = rest.IndexOf(')');

        if (index > 0)
          path = rest.Substring(0, index);

        if (func == "group")
        {
          var grouping = path.Split(',');
          groupingInterval = TimeSpan.Parse(grouping[0]);
          var groupNameFormat = grouping[1];
          dataGroupNames = new List<string>();
          dataGroupValues = new List<double>();

          var counter = DateTime.Parse(from.ToShortDateString());
          while (counter <= to)
          {
            dataGroupNames.Add(counter.ToString(groupNameFormat));
            dataGroupValues.Add(counter.Ticks);
            counter += groupingInterval;
          }

          continue;
        }



        index = rest.IndexOfAny(new char[] { '*', '/' });
        if (index >= 0)
        {
          scale = double.Parse(rest.Substring(index + 1));
          if (rest[index] == '/')
            scale = 1 / scale;
        }

        var ds = new DataSeries(METHOD_NAMES[func] + " " + path);
        var dt = from;
        while (dt <= to)
        {
          var next = dt + timespan;
          var messages = data.Where(x => x.DateTime >= dt && x.DateTime < next);

          if (func == "count")
          {
            ds.DataPoints.Add(new DataPoint(messages.Count() * scale, dt.Ticks, dt.ToString(dateFormat)));
          }
          else
          {
            IEnumerable<decimal> values;
            if (string.IsNullOrEmpty(path))
            {
              values = messages.Select(x => (decimal)0);
            }
            else
            {
              values = messages.Select(x => getValue(x, path));
            }

            ds.DataPoints.Add(new DataPoint((double)METHODS[func](values) * scale, dt.Ticks, dt.ToString(dateFormat)));
          }

          dt = next;
        }

        dseries.Add(ds);
      }

      var res = new DataCollection(name, $"[{string.Join(", ", series)}] of {name}", null, null, dseries.ToArray());
      if (dataGroupNames != null)
      {
        res.DataGroupValues.AddRange(dataGroupValues);
        res.DataGroupNames.AddRange(dataGroupNames);
      }
      return res;
    }

    private decimal getValue(object obj, string path)
    {
      foreach (var p in path.Split('.'))
      {
        obj = getObject(obj, p);
      }

      return Convert.ToDecimal(obj);
    }

    private object getObject(object obj, string name)
    {
      var type = obj.GetType();
      var prop = type.GetProperties(BindingFlags.Public | BindingFlags.Instance).FirstOrDefault(x => x.Name.ToLower() == name);
      if (prop != null)
        return prop.GetValue(obj);

      return null;
    }
  }
}
