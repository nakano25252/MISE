using System.Text.Json.Serialization;

namespace RetailCanvas.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ElementKind
{
	Text,
	Image,
	Shape,
	QrCode
}
