using UnityEngine;

/// <summary>
/// 收银区(处理玩家进入收银区时，自动触发收银台的“结账”逻辑)挂在收银台后面的黄格子 Trigger 上
/// </summary>
public class CashRegisterArea : MonoBehaviour
{
    public CashDesk cashDesk;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") || other.CompareTag("CashierStaff"))
        {
            cashDesk.SetPlayerPresence(true);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player") || other.CompareTag("CashierStaff"))
        {
            cashDesk.SetPlayerPresence(false);
        }
    }
}