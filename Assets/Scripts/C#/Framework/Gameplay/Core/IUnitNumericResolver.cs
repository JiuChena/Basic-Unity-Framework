/// <summary>
/// 瑙掕壊鏁板€艰В鏋愬绾︺€?/// 杩愯鏃跺彧鍏冲績鈥滄寜鏁板€?key 涓庣瓑绾ф潵婧愯В鏋愮粨鏋溾€濓紝涓嶅叧蹇冮」鐩叿浣撲娇鐢ㄥ摢绉?ScriptableObject 鎴栨暟鎹〃缁撴瀯銆?/// </summary>
public interface IUnitNumericResolver
{
    bool TryResolveValue(string key, IUnitAbilityLevelProvider levelProvider, out float value);
    bool ContainsKey(string key);
}
