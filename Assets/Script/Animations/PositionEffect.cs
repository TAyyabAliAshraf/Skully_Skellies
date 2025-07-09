using UnityEngine;
using DG.Tweening;

public class PositionEffect : MonoBehaviour
{
    [Header("Position Animation Settings")]
    public Vector3 fromPosition;
    public Vector3 toPosition;
    public float duration = 0.3f;
    public float delay = 0f;

    [Header("Options")]
    public bool loop = false;
    public bool goBack = false;

    private Tween positionTween;

    void Start()
    {
        PlayPositionAnimation();
    }

    public void PlayPositionAnimation()
    {
        // Kill existing animation
        positionTween?.Kill();

        // Start at fromPosition
        transform.localPosition = fromPosition;

        if (loop)
        {
            positionTween = transform.DOLocalMove(toPosition, duration)
                .SetDelay(delay)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo);
        }
        else if (goBack)
        {
            Sequence seq = DOTween.Sequence();
            seq.AppendInterval(delay);
            seq.Append(transform.DOLocalMove(toPosition, duration).SetEase(Ease.OutQuad));
            seq.Append(transform.DOLocalMove(fromPosition, duration).SetEase(Ease.InQuad));
            positionTween = seq;
        }
        else
        {
            positionTween = transform.DOLocalMove(toPosition, duration)
                .SetDelay(delay)
                .SetEase(Ease.OutQuad);
        }
    }

    public void StopPositionAnimation()
    {
        positionTween?.Kill();
        transform.localPosition = fromPosition;
    }
}
