using System;
using ScummEditor.Engine.Structures;
using Xunit;

namespace ScummEditor.UnitTests
{
    /// <summary>
    /// Guards the game display-name mapping (the bug where Indiana Jones 3 / Zak / Maniac showed as
    /// "None" in the status bar because GetGameName had no case for them).
    /// </summary>
    public class ScummGameNamesTests
    {
        [Fact]
        public void EveryGameHasARealDisplayName()
        {
            foreach (ScummGame game in Enum.GetValues(typeof(ScummGame)))
            {
                string name = ScummGameNames.DisplayName(game);
                Assert.False(string.IsNullOrWhiteSpace(name), game + " has no display name");
                if (game != ScummGame.None)
                    Assert.NotEqual("None", name); // a real game must never display as "None"
            }
        }

        [Theory]
        [InlineData(ScummGame.IndianaJones3, "Indiana Jones and the Last Crusade")]
        [InlineData(ScummGame.ZakMcKracken, "Zak McKracken and the Alien Mindbenders")]
        [InlineData(ScummGame.ManiacMansion, "Maniac Mansion")]
        public void PreviouslyUnmappedGamesNowNamed(ScummGame game, string expected)
        {
            Assert.Equal(expected, ScummGameNames.DisplayName(game));
        }
    }
}
