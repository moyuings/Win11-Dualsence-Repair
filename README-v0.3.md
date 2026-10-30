# v0.3 原生触觉通道实验版

新增“恢复 PS5 原生触觉通道”和“PS5 高清触觉测试（左→右）”。
恢复操作：先确认唯一 USB DualSense 及四声道音频端点，再释放普通震动模式、备份并恢复手柄音量至 100%、取消静音。
测试使用 WASAPI 共享模式直接向第 3、4 声道发送短时低强度波形；前两个扬声器声道为零。结束后停止并释放音频流，不驻留、不持续抢占游戏。

使用：点击恢复原生触觉通道，再点击高清触觉测试，确认左右握把不同纹理。随后进入游戏任务验证。普通震动测试单独保留。

限制：这恢复的是手柄端原生触觉输出条件，不会让游戏凭空生成高清触觉数据，不保证修复游戏自身的触觉开关、效果生成或路由错误。不把系统声音转换成假游戏触觉。不修改存档、游戏、Steam 设置、驱动或默认声音设备。原音量可用“恢复原手柄音量”撤销。

验证：本机唯一 USB DualSense，4 声道 48000Hz 32bit；WASAPI 开流、左右声道发送、排空、停止均通过。实际手感及 AC8 内效果待用户确认。
协议参考：https://github.com/libsdl-org/SDL/blob/main/src/joystick/hidapi/SDL_hidapi_ps5.c
WASAPI：https://learn.microsoft.com/en-us/windows/win32/api/audioclient/nf-audioclient-iaudioclient-initialize
