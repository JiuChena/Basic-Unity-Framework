using System;
using System.Buffers.Binary;
using System.Collections.Generic;

/// <summary>
/// 网络消息注册表：为消息类型分配 uint 网络 ID，并提供「ID 头 + 消息体」的打包与解析。
/// </summary>
/// <remarks>
/// 数据包格式：前 4 字节为大端消息 ID，其后为消息体字节流。
/// 线程安全约束：Register 须在单线程初始化阶段全部完成；此后多线程读取安全。
/// </remarks>
public static class InternetMessage
{
    // 消息 ID 到消息类型的映射。
    private static Dictionary<uint, Type> ID_Type = new();
    // 消息类型到消息 ID 的映射。
    private static Dictionary<Type, uint> Type_ID = new();

    /// <summary>
    /// 注册消息类型与其网络 ID 的双向映射。
    /// </summary>
    /// <param name="id">消息网络 ID，全局唯一。</param>
    /// <param name="type">消息类型，全局唯一。</param>
    /// <exception cref="InvalidOperationException">ID 或类型已被注册时抛出。</exception>
    public static void Register(uint id, Type type)
    {
        // 先完成双侧存在性检查再写入，避免一侧写入成功后另一侧冲突导致半注册状态。
        if (ID_Type.ContainsKey(id)) throw new InvalidOperationException($"ID {id} 已被 {ID_Type[id].FullName} 注册");
        if (Type_ID.ContainsKey(type)) throw new InvalidOperationException($"类型 {type.FullName} 已被 ID {Type_ID[type]} 注册");

        ID_Type.Add(id, type);
        Type_ID.Add(type, id);
    }

    /// <summary>
    /// 按网络 ID 获取已注册的消息类型。
    /// </summary>
    /// <param name="id">消息网络 ID。</param>
    /// <returns>对应的消息类型。</returns>
    /// <exception cref="InvalidOperationException">ID 未注册时抛出。</exception>
    public static Type GetMessageType(uint id)
    {
        if (ID_Type.TryGetValue(id, out Type type)) return type;
        throw new InvalidOperationException($"消息 ID {id} 未注册");
    }

    /// <summary>
    /// 解析完整数据包：读取头部 ID 得到消息类型，并把 <paramref name="bytes"/> 剥离为纯消息体。
    /// </summary>
    /// <param name="bytes">完整数据包（前 4 字节为大端消息 ID）；调用后仅剩消息体部分。</param>
    /// <returns>解析出的消息类型。</returns>
    /// <exception cref="InvalidOperationException">数据包长度不足 4 字节，或 ID 未注册时抛出。</exception>
    public static Type GetMessageType(ref byte[] bytes)
    {
        if (bytes == null || bytes.Length < 4) throw new InvalidOperationException("数据包长度不足 4 字节，无法解析消息 ID");

        // 解读头部 ID 并返回信息类型（必须先读 ID 再剥离，顺序颠倒会读到消息体）。
        uint id = BinaryPrimitives.ReadUInt32BigEndian(bytes);
        Type type = GetMessageType(id);

        // 分离 ID 数据流与信息数据流：调用后 bytes 仅剩消息体。
        bytes = bytes[4..];
        return type;
    }

    /// <summary>
    /// 在消息体前打包 4 字节大端消息 ID，生成完整数据包。
    /// </summary>
    /// <typeparam name="T">消息类型，须已注册。</typeparam>
    /// <param name="bytes">纯消息体字节数组。</param>
    /// <returns>带 ID 头的完整数据包。</returns>
    /// <exception cref="InvalidOperationException">消息类型未注册时抛出。</exception>
    public static byte[] MessageIDPush<T>(byte[] bytes)
    {
        if (!Type_ID.TryGetValue(typeof(T), out uint id)) throw new InvalidOperationException($"{typeof(T).FullName} 未注册，请先调用 {nameof(Register)}");

        // 整合 ID 与信息字节数组：前 4 字节大端 ID + 消息体。
        byte[] finalBytes = new byte[4 + bytes.Length];
        BinaryPrimitives.WriteUInt32BigEndian(finalBytes, id);
        bytes.CopyTo(finalBytes, 4);

        return finalBytes;
    }
}
