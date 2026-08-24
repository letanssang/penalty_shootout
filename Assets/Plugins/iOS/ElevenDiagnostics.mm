// Eleven Metres — plugin native iOS cho PerfHud.
// Trả về trạng thái nhiệt của máy theo thang NSProcessInfoThermalState:
// 0 = nominal, 1 = fair, 2 = serious, 3 = critical — khớp contract thermalState 0..3.
#import <Foundation/Foundation.h>

extern "C" int ElevenNative_ThermalState(void)
{
    if (@available(iOS 11.0, *))
    {
        return (int)[[NSProcessInfo processInfo] thermalState];
    }
    return 0;
}
