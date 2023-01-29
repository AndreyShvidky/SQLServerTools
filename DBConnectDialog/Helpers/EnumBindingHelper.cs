using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows.Data;
using System.Windows.Markup;

namespace EnumBindingHelper
{
	public class EnumElement
	{

		public int Value { get; set; }
		public string Name { get; set; }
		public string Description { get; set; }

		public EnumElement(int paramValue, string paramName, string paramDescription)
		{
			Value = paramValue;
			Name = paramName;
			Description = paramDescription;
		}
	}

	public static class EnumExtensions
	{
		/// <summary>
		/// Gets an attribute on an enum field value
		/// </summary>
		/// <typeparam name="T">The type of the attribute you want to retrieve</typeparam>
		/// <param name="enumVal">The enum value</param>
		/// <returns>The attribute of type T that exists on the enum value</returns>
		/// <example>string desc = myEnumVariable.GetAttributeOfType<DescriptionAttribute>().Description;</example>
		/// На самом деле это не совсем расширение... enumValue.GetAttributeOfType не работает. Оставим это профессионалам.
		public static T GetAttributeOfType<T>(this Enum enumVal) where T : System.Attribute
		{
			var type = enumVal.GetType();
			var memInfo = type.GetMember(enumVal.ToString());
			var attributes = memInfo[0].GetCustomAttributes(typeof(T), false);
			return (attributes.Length > 0) ? (T)attributes[0] : null;
		}

		public static IEnumerable<EnumElement> AsBindableEnumerable(this Type pEnum)
		{
			if (!pEnum.IsEnum) throw new ArgumentException($"Тип не является Enum.", nameof(pEnum));
			EnumElement enumMember;

			foreach (var enumValue in Enum.GetValues(pEnum))
			{
				enumMember = new EnumElement((int)enumValue, Enum.GetName(pEnum, enumValue), GetAttributeOfType<DescriptionAttribute>((Enum)enumValue).Description);
				yield return enumMember;
			}
		}
	}

	public class EnumBindingSourceExtension : MarkupExtension
	{
		private Type _enumType;
		public Type EnumType
		{
			get { return this._enumType; }
			set
			{
				if (value != this._enumType)
				{
					if (null != value)
					{
						Type enumType = Nullable.GetUnderlyingType(value) ?? value;
						if (!enumType.IsEnum)
							throw new ArgumentException("Тип должен быть Enum.", nameof(enumType));
					}

					this._enumType = value;
				}
			}
		}

		public EnumBindingSourceExtension() { }

		public EnumBindingSourceExtension(Type enumType)
		{
			this.EnumType = enumType;
		}

		public override object ProvideValue(IServiceProvider serviceProvider)
		{
			if (this._enumType == null)
				throw new InvalidOperationException("Не задан Enum.");

			Type actualEnumType = Nullable.GetUnderlyingType(this._enumType) ?? this._enumType;

			//return actualEnumType.AsBindableEnumerable();
			List<EnumElement> enumList = actualEnumType.AsBindableEnumerable().ToList();
			return enumList;
		}
	}

	public class EnumToIntConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return (int)value;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return Enum.ToObject(targetType, value);
		}
	}

	public class EnumToDescriptionConverter : IValueConverter
	{
		public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return EnumExtensions.GetAttributeOfType<DescriptionAttribute>((Enum)value).Description;
			//return value.GetType().GetCustomAttribute<DescriptionAttribute>().Description;
		}

		public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
		{
			return new NotImplementedException();
		}
	}
}
