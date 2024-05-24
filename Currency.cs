using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace jgmamxmn
{
	/// <summary>
	/// Currency type. Don't use decimal types for currency, everyone knows that.
	/// Note: this assumes a period character (.) for decimal separation and a comma (,) for digit grouping (e.g. thousands).
	/// This should be considered case-by-case: one option would be to use the system locale to determine the separator chars,
	/// but it depends more on the formatting of the source data, which may be different.
	/// Normal usage is to create a Currency object by calling Currency.TryParse().
	/// You can then access the major and minor values (e.g. dollars and cents) from the generated objects Major and Minor properties.
	/// 
	/// </summary>
	public class Currency
	{
		public int Major;
		public uint Minor;

		public Currency() { }
		public Currency(int major, uint minor)
		{
			Major = major;
			Minor = minor;
		}


		/// <summary>
		/// Let's assume full stop is decimal and comma is thousands (see comment against class definition above)
		/// </summary>
		private const char DecimalSeparator = '.';
		private const char ThousandsSeparator = ','; // Or lakh separator, or whatever
		private const ushort MinorCurrencyLength = 2;
		override public string ToString() => ToString(MinorCurrencyLength);
		public string ToString(uint currencyPlaces)
		{
			// Make sure that the minor currency doesn't have too many decimal places.
			if ((currencyPlaces == 0 && Minor != 0) || Minor >= IntPow(10, currencyPlaces))
				throw new ArithmeticException($"Minor currency value {Minor} is too big for the {currencyPlaces} permitted decimal places.");

			if (currencyPlaces == 0)
				return ToDelimitedString(Major);
			else
				return ToDelimitedString(Major) + DecimalSeparator + Minor.ToString($"D{currencyPlaces}");
		}

		private static string ToDelimitedString(int i)
		{
			var s = new List<char>(i.ToString());
			int x = s.Count - 3;
			while(x > 0)
			{
				s.Insert(x, ThousandsSeparator);
				x -= 3;
			}
			return new string(s.ToArray());
		}

		/// <summary>
		/// Returns x^pow
		/// </summary>
		/// <param name="x"></param>
		/// <param name="pow"></param>
		/// <returns></returns>
		private static int IntPow(int x, uint pow)
		{
			int ret = 1;
			while (pow != 0)
			{
				if ((pow & 1) == 1)
					ret *= x;
				x *= x;
				pow >>= 1;
			}
			return ret;
		}

		public static bool TryParse(string MajorAndMinor, out Currency Result)
		{
			Result = null;

			var Split = MajorAndMinor?.Split(new[] { '.' }, 2);
			var Len = Split?.Length ?? 0;

			if (Len < 0 || Len > 2) return false;

			int Major; uint Minor = 0;
			// Parse major
			if (!int.TryParse(Split[0].Replace(new string(new[] { ThousandsSeparator }), ""), out Major))
				return false;
			// Parse minor
			if (Len == 2)
			{
				var sMinor = Split[1];
				// If the minor part is too short, pad it with zeroes (e.g. "2.5" should be parsed as $2 + 50c, not $2 + 5c)
				while (sMinor.Length < MinorCurrencyLength)
					sMinor += '0';
				if (!uint.TryParse(sMinor, out Minor))
					return false;
			}

			Result = new Currency(Major, Minor);
			return true;
		}

		public static Currency Parse(string MajorAndMinor)
		{
			if (TryParse(MajorAndMinor, out Currency Result))
				return Result;
			throw new ArgumentException($"Cannot parse '{MajorAndMinor}' to currency type.");
		}

		/// <summary>
		/// Compares major and minor parts of the currency
		/// </summary>
		public static bool operator ==(Currency A, Currency B) => (A.Major == B.Major && A.Minor == B.Minor);
		/// <summary>
		/// Compares major and minor parts of the currency
		/// </summary>
		public static bool operator !=(Currency A, Currency B) => !(A == B);

		public override bool Equals(object obj)
		{
			if (obj is Currency C)
				return (this == C);
			return base.Equals(obj);
		}
		public override int GetHashCode()
		{
			return ToString().GetHashCode();
		}


		public static implicit operator Currency(string MajorAndMinor)
		{
			return Currency.Parse(MajorAndMinor);
		}
		public static implicit operator Currency(double MajorAndMinor)
		{
			var Major = (int)MajorAndMinor;
			return new Currency
			{
				Major = Major,
				Minor = (uint)((MajorAndMinor - Major) * IntPow(10, MinorCurrencyLength))
			};
		}
		public static implicit operator Currency(decimal MajorAndMinor)
		{
			var Major = (int)MajorAndMinor;
			return new Currency
			{
				Major = Major,
				Minor = (uint)((MajorAndMinor - Major) * IntPow(10, MinorCurrencyLength))
			};
		}
	}
}

