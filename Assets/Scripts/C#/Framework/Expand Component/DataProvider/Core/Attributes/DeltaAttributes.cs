using UnityEngine;

namespace Framework.ExpandComponent.DataProvider
{
    /// <summary>
    /// Vector2 增量属性的消费者游标。
    /// 通过 CreateCursor 创建，新游标从创建时刻的累计值开始，不回放历史增量。
    /// </summary>
    public struct Vector2DeltaCursor
    {
        internal double x;
        internal double y;
    }

    /// <summary>
    /// int 增量属性的消费者游标。
    /// 通过 CreateCursor 创建，新游标从创建时刻的累计值开始，不回放历史增量。
    /// </summary>
    public struct IntDeltaCursor
    {
        internal long total;
    }

    /// <summary>
    /// 可被多个消费者独立读取的二维累计增量属性基类。
    /// Provider 每帧调用 Add 追加增量，每个消费者保存自己的游标，
    /// 调用 Consume 获得自上次读取后的总增量，互不干扰。
    /// </summary>
    public abstract class Vector2DeltaAttribute : BlackboardAttribute
    {
        private double _totalX;
        private double _totalY;

        /// <summary>累计增量快照，仅用于诊断，业务读取应使用 Consume。</summary>
        public Vector2 Total => new Vector2(ToFloat(_totalX), ToFloat(_totalY));

        /// <summary>
        /// 追加本帧产生的增量。
        /// </summary>
        /// <param name="delta">本帧新增的二维增量</param>
        public void Add(Vector2 delta)
        {
            _totalX += delta.x;
            _totalY += delta.y;
        }

        /// <summary>
        /// 创建从当前时刻开始读取的新消费者游标，不回放历史增量。
        /// 适用于动态创建或禁用的消费者在重新启用时获取正确起点。
        /// </summary>
        public Vector2DeltaCursor CreateCursor()
        {
            return new Vector2DeltaCursor { x = _totalX, y = _totalY };
        }

        /// <summary>
        /// 读取并推进指定消费者自上次消费后的累计增量。
        /// </summary>
        /// <param name="cursor">消费者持有的游标，调用后推进到当前累计值</param>
        /// <param name="delta">自上次消费后的累计增量</param>
        /// <returns>增量非零时返回 true</returns>
        public bool Consume(ref Vector2DeltaCursor cursor, out Vector2 delta)
        {
            double x = _totalX - cursor.x;
            double y = _totalY - cursor.y;
            cursor.x = _totalX;
            cursor.y = _totalY;
            delta = new Vector2(ToFloat(x), ToFloat(y));
            return x * x + y * y > 0.000001d;
        }

        private static float ToFloat(double value)
        {
            return value > float.MaxValue
                ? float.MaxValue
                : value < float.MinValue
                    ? float.MinValue
                    : (float)value;
        }
    }

    /// <summary>
    /// 可被多个消费者独立读取的整数累计增量属性基类。
    /// 使用 long 保存总量，防止长时间运行时累计值溢出。
    /// </summary>
    public abstract class IntDeltaAttribute : BlackboardAttribute
    {
        private long _total;

        /// <summary>累计增量快照，仅用于诊断，业务读取应使用 Consume。</summary>
        public long Total => _total;

        /// <summary>
        /// 追加本帧产生的增量。
        /// </summary>
        /// <param name="delta">本帧新增的整数增量</param>
        public void Add(int delta)
        {
            _total += delta;
        }

        /// <summary>
        /// 创建从当前时刻开始读取的新消费者游标，不回放历史增量。
        /// </summary>
        public IntDeltaCursor CreateCursor()
        {
            return new IntDeltaCursor { total = _total };
        }

        /// <summary>
        /// 读取并推进指定消费者自上次消费后的累计增量。
        /// </summary>
        /// <param name="cursor">消费者持有的游标，调用后推进到当前累计值</param>
        /// <param name="delta">自上次消费后的累计增量，溢出时自动钳制到 int 范围</param>
        /// <returns>增量非零时返回 true</returns>
        public bool Consume(ref IntDeltaCursor cursor, out int delta)
        {
            long difference = _total - cursor.total;
            cursor.total = _total;
            delta = difference > int.MaxValue
                ? int.MaxValue
                : difference < int.MinValue
                    ? int.MinValue
                    : (int)difference;
            return difference != 0;
        }
    }
}
