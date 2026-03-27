namespace DcMateClassLibrary.Helper
{
    /// <summary>
    /// 提供 SID 與雪花 ID 產生功能。
    /// </summary>
    public static class RandomHelper
    {
        /// <summary>
        /// 舊 H5Core SID 預設起始時間。
        /// </summary>
        private static readonly DateTime DefaultSidStartDate = new(2023, 4, 1, 0, 0, 0, DateTimeKind.Local);

        /// <summary>
        /// 同步鎖，確保自動序號模式在多執行緒下不會重複。
        /// </summary>
        private static readonly object SidLock = new();

        /// <summary>
        /// 上一次產 SID 時使用的毫秒時間戳。
        /// </summary>
        private static long _lastTimestampMilliseconds = -1;

        /// <summary>
        /// 同一毫秒內的自增序號。
        /// </summary>
        private static int _sequence;

        /// <summary>
        /// 單一毫秒內可用的最大序號。
        /// 對齊舊邏輯保留 3 位數空間：000 ~ 999。
        /// </summary>
        private const int MaxSequence = 999;

        /// <summary>
        /// 依照舊 H5Core 規則產生 SID。
        /// SID = [起始日至今分鐘數] * 100000000 + [秒] * 1000000 + [毫秒] * 1000 + [序號]
        /// </summary>
        /// <param name="sequence">
        /// 可選。若有指定，使用指定序號；若未指定，則於同一毫秒內自動遞增。
        /// </param>
        /// <returns>符合舊系統規則的 SID。</returns>
        /// <exception cref="ArgumentOutOfRangeException">當 sequence 超出 0~999 時拋出。</exception>
        /// <exception cref="ArgumentException">當有效起始時間晚於目前時間時拋出。</exception>
        public static decimal GenerateRandomDecimal(int? sequence = null)
        {
            DateTime effectiveStartDate = DefaultSidStartDate;
            DateTime now = DateTime.Now;

            if (effectiveStartDate > now)
            {
                throw new ArgumentException("startDate 不可晚於目前時間。", nameof(DefaultSidStartDate));
            }

            int actualSequence;

            if (sequence.HasValue)
            {
                if (sequence.Value < 0 || sequence.Value > MaxSequence)
                {
                    throw new ArgumentOutOfRangeException(nameof(sequence), sequence.Value, "sequence 必須介於 0 到 999 之間。");
                }

                actualSequence = sequence.Value;
            }
            else
            {
                lock (SidLock)
                {
                    now = DateTime.Now;
                    long currentTimestampMilliseconds = new DateTimeOffset(now).ToUnixTimeMilliseconds();

                    if (currentTimestampMilliseconds == _lastTimestampMilliseconds)
                    {
                        _sequence++;

                        if (_sequence > MaxSequence)
                        {
                            now = WaitNextMillisecond(currentTimestampMilliseconds);
                            currentTimestampMilliseconds = new DateTimeOffset(now).ToUnixTimeMilliseconds();
                            _lastTimestampMilliseconds = currentTimestampMilliseconds;
                            _sequence = 0;
                        }
                    }
                    else
                    {
                        _lastTimestampMilliseconds = currentTimestampMilliseconds;
                        _sequence = 0;
                    }

                    actualSequence = _sequence;
                }
            }

            TimeSpan duration = now - effectiveStartDate;
            long totalMinutes = (long)duration.TotalMinutes;

            decimal sid =
                totalMinutes * 100000000M +
                now.Second * 1000000M +
                now.Millisecond * 1000M +
                actualSequence;

            return sid;
        }

        /// <summary>
        /// 產生類似 Snowflake 的唯一遞增 ID。
        /// 結構：
        /// [UnixTimeMilliseconds 左移 22 bit] | [低 22 bit 序號]
        /// </summary>
        /// <returns>唯一且大致遞增的 long ID。</returns>
        /// <exception cref="InvalidOperationException">當單一毫秒內請求量超過可容納上限時拋出。</exception>
        public static long NextSnowflakeId()
        {
            lock (SidLock)
            {
                long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                if (timestamp == _lastTimestampMilliseconds)
                {
                    _sequence++;

                    if (_sequence >= (1 << 22))
                    {
                        throw new InvalidOperationException("同一毫秒內產生的 ID 已超過 22-bit 可容納上限。");
                    }
                }
                else
                {
                    _lastTimestampMilliseconds = timestamp;
                    _sequence = 0;
                }

                return (timestamp << 22) | (uint)_sequence;
            }
        }

        /// <summary>
        /// 等待到下一個毫秒。
        /// </summary>
        /// <param name="currentTimestampMilliseconds">目前毫秒時間戳。</param>
        /// <returns>下一個毫秒對應的本地時間。</returns>
        private static DateTime WaitNextMillisecond(long currentTimestampMilliseconds)
        {
            DateTime now;

            do
            {
                Thread.SpinWait(20);
                now = DateTime.Now;
            }
            while (new DateTimeOffset(now).ToUnixTimeMilliseconds() <= currentTimestampMilliseconds);

            return now;
        }
    }
}