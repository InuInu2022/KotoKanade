namespace KotoKanade.Core.Models;

public record TalkGlobalParam
{
	public decimal AlphaShift { get; init;}
	/// <summary>
	/// Vol.
	/// </summary>
	public decimal C0Shift { get; init; }
	/// <summary>
	/// Into.
	/// </summary>
	public decimal LogF0Scale { get; init; }
	/// <summary>
	/// Pitch
	/// </summary>
	public decimal LogF0Shift { get; init; }
	/// <summary>
	/// Speed
	/// </summary>
	public decimal SpeedRatio { get; init; }
}