using UnityEngine;

namespace Sokoban
{
    public static class RuntimeGameBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Object.FindObjectOfType<GameController>() != null)
            {
                return;
            }

            GameObject gameController = new GameObject("Sokoban Game Controller");
            gameController.AddComponent<GameController>();
        }
    }
}
