using UnityEngine;

// Odsvira zvuk u trenutku kad partija zavrsi.
public class GameOverSound : MonoBehaviour
{
    public AudioSource source;
    public AudioClip gameOverSound;

    private bool played;

    void Update()
    {
        if (played) return;

        GameResources resources = GameResources.Instance;
        if (resources == null || !resources.gameOver) return;

        played = true;
        if (source != null && gameOverSound != null)
            source.PlayOneShot(gameOverSound);
    }
}
