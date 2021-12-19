namespace Tarot
{
	public static class Utils
	{
		private static int totalCards = System.Enum.GetValues(typeof(Tarot.AllCards)).Length;
		public static int TotalCards { get { return totalCards; } }
		public static AllCards GetValue(int token_id)
		{
			return (AllCards)(token_id % totalCards);
		}
	}

	public enum MinorCards
	{
		ace, two, three, four, five, six, seven, eight, nine, ten, page, knight, queen, king
	}

	public enum MinorSuits
	{
		pentacles, swords, wands, cups
	}

	public enum MinorArcana
	{
		ace_of_pentacles,   two_of_pentacles,   three_of_pentacles,
		four_of_pentacles,  five_of_pentacles,  six_of_pentacles,
		seven_of_pentacles, eight_of_pentacles, nine_of_pentacles,
		ten_of_pentacles,   page_of_pentacles,  knight_of_pentacles,
		queen_of_pentacles, king_of_pentacles,  ace_of_swords,
		two_of_swords,      three_of_swords,    four_of_swords,
		five_of_swords,     six_of_swords,      seven_of_swords,
		eight_of_swords,    nine_of_swords,     ten_of_swords,
		page_of_swords,     knight_of_swords,   queen_of_swords,
		king_of_swords,     ace_of_wands,       two_of_wands,
		three_of_wands,     four_of_wands,      five_of_wands,
		six_of_wands,       seven_of_wands,     eight_of_wands,
		nine_of_wands,      ten_of_wands,       page_of_wands,
		knight_of_wands,    queen_of_wands,     king_of_wands,
		ace_of_cups,        two_of_cups,        three_of_cups,
		four_of_cups,       five_of_cups,       six_of_cups,
		seven_of_cups,      eight_of_cups,      nine_of_cups,
		ten_of_cups,        page_of_cups,       knight_of_cups,
		queen_of_cups,      king_of_cups
	}

	public enum MajorArcana
	{
		the_fool, the_magician, high_priestess, the_empress, the_emperor, the_hierophant, the_lovers,
		the_chariot, strength, the_hermit, wheel_of_fortune, justice, hanged_man, death, temperance,
		the_devil, the_tower, the_star, the_moon, the_sun, judgment, the_world
	}

	public enum AllCards
	{
		ace_of_pentacles,   two_of_pentacles,   three_of_pentacles,
		four_of_pentacles,  five_of_pentacles,  six_of_pentacles,
		seven_of_pentacles, eight_of_pentacles, nine_of_pentacles,
		ten_of_pentacles,   page_of_pentacles,  knight_of_pentacles,
		queen_of_pentacles, king_of_pentacles,  ace_of_swords,
		two_of_swords,      three_of_swords,    four_of_swords,
		five_of_swords,     six_of_swords,      seven_of_swords,
		eight_of_swords,    nine_of_swords,     ten_of_swords,
		page_of_swords,     knight_of_swords,   queen_of_swords,
		king_of_swords,     ace_of_wands,       two_of_wands,
		three_of_wands,     four_of_wands,      five_of_wands,
		six_of_wands,       seven_of_wands,     eight_of_wands,
		nine_of_wands,      ten_of_wands,       page_of_wands,
		knight_of_wands,    queen_of_wands,     king_of_wands,
		ace_of_cups,        two_of_cups,        three_of_cups,
		four_of_cups,       five_of_cups,       six_of_cups,
		seven_of_cups,      eight_of_cups,      nine_of_cups,
		ten_of_cups,        page_of_cups,       knight_of_cups,
		queen_of_cups,      king_of_cups,       the_fool,
		the_magician,       high_priestess,     the_empress,
		the_emperor,        the_hierophant,     the_lovers,
		the_chariot,        strength,           the_hermit,
		wheel_of_fortune,   justice,            hanged_man,
		death,              temperance,         the_devil,
		the_tower,          the_star,           the_moon,
		the_sun,            judgment,           the_world
	}
};

