using UnityEngine;

public class LoadingAnimation : MonoBehaviour
{
    public RectTransform rectTransform;
    public float rotationSpeed = 270f;
    public float cycleDuration = 2f; // Time to go from 0 to max and back

    private float timeElapsed = 0f;

    void Start()
    {
        rectTransform = GetComponent<RectTransform>();
    }

    void Update()
    {
        timeElapsed += Time.deltaTime;
        float t = (timeElapsed % cycleDuration) / cycleDuration;
        float easedSpeed = Mathf.SmoothStep(0, 1, t < 0.5f ? t * 2 : 2 - t * 2);
        rectTransform.Rotate(0f, 0f, -rotationSpeed * easedSpeed * Time.deltaTime);
    }
}
