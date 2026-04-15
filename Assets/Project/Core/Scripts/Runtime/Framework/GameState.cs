using System;

namespace Project.Core.Runtime.Framework
{
    public enum GameState
    {
        None = 0,
        Init = 1,
        Title = 2,
        Exploration = 3,
        Inspection = 4,
        Pause = 5,
        GameOver = 6,
        Victory = 7
    }
}
