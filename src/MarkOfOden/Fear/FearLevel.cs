namespace MarkOfOden.Fear
{
	/// <summary>How a single creature feels about a single player, right now.</summary>
	public enum FearLevel
	{
		/// <summary>Vanilla behaviour. The creature has no idea who it is looking at.</summary>
		Normal = 0,

		/// <summary>Will not pick the player as a target, but holds its ground and goes about its business.</summary>
		Cautious = 1,

		/// <summary>Actively runs away.</summary>
		Afraid = 2,

		/// <summary>Runs, and cowers instead when cornered or when the player is right on top of it.</summary>
		Terrified = 3
	}
}
