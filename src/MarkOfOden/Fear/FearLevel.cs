namespace MarkOfOden.Fear
{
	/// <summary>How a single creature feels about a single player, right now.</summary>
	/// <remarks>
	/// Two questions used to be answered by one scale: whether a creature picks a fight with you, and
	/// whether it runs from you. They are different decisions, and animals do not make them the same
	/// way - a wolf that knows you are dangerous keeps its distance rather than bolting at the sight of
	/// you, and runs only when a fight is going badly for it. So your standing now decides only the
	/// first question, and <see cref="Morale"/> decides the second, during a fight and never before one.
	/// </remarks>
	public enum FearLevel
	{
		/// <summary>Vanilla behaviour. The creature has no idea who it is looking at.</summary>
		Normal = 0,

		/// <summary>Will not pick the player as a target, but holds its ground and goes about its business.</summary>
		Cautious = 1,

		/// <summary>Was losing a fight against someone who outranks it, and has broken and is running.</summary>
		Broken = 2
	}
}
