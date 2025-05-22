using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneControl : MonoBehaviour
{
    public void LoadSetPieceAnalysisScene()
    {
        SceneManager.LoadScene("3D Trajectories");
    }

    public void LoadReplayScene()
    {
        SceneManager.LoadScene("Replay");
    }
}
