using System;
using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;
using Random = UnityEngine.Random;

public class ShakeCamera : MonoBehaviour
{
    private CancellationTokenSource cts;

    /// <summary>
    /// Gọi hàm này để bắt đầu hiệu ứng rung camera bằng UniTask.
    /// </summary>
    /// <param name="duration">Thời gian rung</param>
    /// <param name="magnitude">Độ mạnh của rung</param>
    public void StartShake(float duration, float magnitude, Action start, Action end)
    {
        // Hủy nhiệm vụ cũ nếu đang chạy
        cts?.Cancel();
        cts = new CancellationTokenSource();

        // Lưu vị trí hiện tại làm gốc mỗi lần gọi
        Vector3 basePosition = transform.localPosition;
        Shake(duration, magnitude, basePosition, cts.Token, start, end).Forget();
    }

    private async UniTaskVoid Shake(float duration, float magnitude, Vector3 basePosition, CancellationToken token,
        Action start, Action end)
    {
        start?.Invoke();
        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (token.IsCancellationRequested) return;

            Vector3 randomPoint = basePosition + Random.insideUnitSphere * magnitude;
            transform.localPosition = randomPoint;

            elapsed += Time.unscaledDeltaTime;
            await UniTask.Yield(PlayerLoopTiming.Update, token);
        }

        transform.localPosition = basePosition;
        end?.Invoke();
    }



    private void OnDestroy()
    {
        cts?.Cancel();
        cts?.Dispose();
    }
}