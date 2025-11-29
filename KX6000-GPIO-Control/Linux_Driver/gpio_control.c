#include <stdio.h>
#include <stdlib.h>
#include <fcntl.h>
#include <unistd.h>
#include <sys/ioctl.h>
#include <termios.h>
#include <string.h>
#include <time.h>
#include <pthread.h>

// IOCTL 命令
#define IOCTL_SET_DIRECTION _IOW('k', 1, int)
#define IOCTL_SET_OUTPUT _IOW('k', 2, int[3])
#define IOCTL_GET_INPUT _IOR('k', 3, int[3])

#define DEVICE_PATH "/dev/f75111_gpio"

// 全局变量
int fd = -1;
int is_input_mode = 1;
pthread_t input_monitor_thread;
int running = 1;

// 函数声明
void cleanup();
void set_gpio_direction(int input_mode);
void set_output(int gpio_num, int state);
void *monitor_inputs(void *arg);
void print_menu();
void print_gpio_state();
int getch();

int main() {
    printf("GPIO控制程序 (F75111芯片) - 内核模块版\n");
    
    // 打开设备
    fd = open(DEVICE_PATH, O_RDWR);
    if (fd < 0) {
        perror("无法打开设备");
        fprintf(stderr, "请确保已加载内核模块: sudo insmod f75111_gpio.ko\n");
        return 1;
    }
    
    // 初始设置为输入模式
    set_gpio_direction(1);
    
    // 创建输入监控线程
    if (pthread_create(&input_monitor_thread, NULL, monitor_inputs, NULL) != 0) {
        perror("无法创建输入监控线程");
        cleanup();
        return 1;
    }
    
    // 主命令循环
    while (running) {
        print_menu();
        
        int choice = getch();
        printf("\n");
        
        switch (choice) {
            case '1': // 切换到输入模式
                set_gpio_direction(1);
                printf("已切换到输入模式\n");
                break;
                
            case '2': // 切换到输出模式
                set_gpio_direction(0);
                printf("已切换到输出模式\n");
                break;
                
            case '3': // 所有输出高电平
                if (!is_input_mode) {
                    for (int i = 1; i <= 8; i++) {
                        set_output(i, 1);
                    }
                    printf("所有GPIO输出高电平\n");
                } else {
                    printf("错误: 当前处于输入模式\n");
                }
                break;
                
            case '4': // 所有输出低电平
                if (!is_input_mode) {
                    for (int i = 1; i <= 8; i++) {
                        set_output(i, 0);
                    }
                    printf("所有GPIO输出低电平\n");
                } else {
                    printf("错误: 当前处于输入模式\n");
                }
                break;
                
            case '5': // 设置单个GPIO输出
                if (!is_input_mode) {
                    printf("输入GPIO编号(1-8): ");
                    int gpio_num;
                    if (scanf("%d", &gpio_num) != 1) {
                        printf("无效的输入\n");
                        while (getchar() != '\n'); // 清除输入缓冲区
                        break;
                    }
                    getchar(); // 消耗换行符
                    
                    if (gpio_num < 1 || gpio_num > 8) {
                        printf("无效的GPIO编号\n");
                        break;
                    }
                    
                    printf("设置状态 (1=高, 0=低): ");
                    int state;
                    if (scanf("%d", &state) != 1) {
                        printf("无效的输入\n");
                        while (getchar() != '\n'); // 清除输入缓冲区
                        break;
                    }
                    getchar(); // 消耗换行符
                    
                    set_output(gpio_num, state);
                    printf("GPIO %d 设置为 %s\n", gpio_num, state ? "高电平" : "低电平");
                } else {
                    printf("错误: 当前处于输入模式\n");
                }
                break;
                
            case '6': // 显示GPIO状态
                print_gpio_state();
                break;
                
            case '0': // 退出
                running = 0;
                break;
                
            default:
                printf("无效选项\n");
        }
        
        printf("\n");
    }
    
    // 清理资源
    cleanup();
    return 0;
}

void cleanup() {
    // 取消输入监控线程
    if (input_monitor_thread) {
        running = 0;
        pthread_join(input_monitor_thread, NULL);
    }
    
    // 关闭设备
    if (fd >= 0) {
        close(fd);
    }
    
    printf("程序已退出\n");
}

void set_gpio_direction(int input_mode) {
    is_input_mode = input_mode;
    if (ioctl(fd, IOCTL_SET_DIRECTION, input_mode) < 0) {
        perror("设置方向失败");
    }
    // 添加延迟确保配置生效
    usleep(200000); // 200ms
    
    // 验证配置
    if (input_mode) {
        printf("已切换到输入模式，等待稳定...\n");
    } else {
        printf("已经换到输出模式\n");
    }
}

void set_output(int gpio_num, int state) {
    int set = (gpio_num <= 6) ? 0 : 1;
    int bit = 0;
    
    // 根据GPIO编号确定位位置
    switch (gpio_num) {
        case 1: bit = 0; break;
        case 2: bit = 1; break;
        case 3: bit = 2; break;
        case 4: bit = 4; break;
        case 5: bit = 6; break;
        case 6: bit = 7; break;
        case 7: bit = 0; break;
        case 8: bit = 1; break;
    }
    
    // 发送命令到内核模块
    int data[3] = {set, (1 << bit), state};
    if (ioctl(fd, IOCTL_SET_OUTPUT, data) < 0) {
        perror("设置输出失败");
    }
    
    // 添加延迟确保输出稳定
    usleep(50000); // 50ms
    
    // 验证输出
    printf("设置 GPIO %d 为 %s - ", 
           gpio_num, state ? "高电平" : "低电平");
    if (state) {
        printf("应测量到高电平\n");
    } else {
        printf("应测量到低电平\n");
    }
    
//    printf("请用万用表验证实际电压...\n");
}

// 输入监控线程
void *monitor_inputs(void *arg) {
    int input_data[2];
    
    while (running) {
        if (is_input_mode && fd >= 0) {
            // 从内核模块读取输入状态
            if (ioctl(fd, IOCTL_GET_INPUT, input_data) < 0) {
                perror("读取输入失败");
            } else {
                // 紧凑显示GPIO状态
                printf("\rGPIO状态: ");
                printf("1:%s 2:%s 3:%s 4:%s 5:%s 6:%s 7:%s 8:%s", 
                       (input_data[0] & (1 << 0)) ? "H" : "L",
                       (input_data[0] & (1 << 1)) ? "H" : "L",
                       (input_data[0] & (1 << 2)) ? "H" : "L",
                       (input_data[0] & (1 << 4)) ? "H" : "L",
                       (input_data[0] & (1 << 6)) ? "H" : "L",
                       (input_data[0] & (1 << 7)) ? "H" : "L",
                       (input_data[1] & (1 << 0)) ? "H" : "L",
                       (input_data[1] & (1 << 1)) ? "H" : "L");
                fflush(stdout);
            }
        }
        
        // 每500ms检查一次
        usleep(500000);
    }
    return NULL;
}

// 打印GPIO状态
void print_gpio_state() {
    int input_data[2] = {0};
    
    if (is_input_mode) {
        if (ioctl(fd, IOCTL_GET_INPUT, input_data) < 0) {
            perror("读取输入失败");
            return;
        }
        
        printf("当前模式: 输入模式\n");
        printf("GPIO1: %s  GPIO2: %s  GPIO3: %s\n", 
               (input_data[0] & (1 << 0)) ? "H" : "L",
               (input_data[0] & (1 << 1)) ? "H" : "L",
               (input_data[0] & (1 << 2)) ? "H" : "L");
        printf("GPIO4: %s  GPIO5: %s  GPIO6: %s\n", 
               (input_data[0] & (1 << 4)) ? "H" : "L",
               (input_data[0] & (1 << 6)) ? "H" : "L",
               (input_data[0] & (1 << 7)) ? "H" : "L");
        printf("GPIO7: %s  GPIO8: %s\n", 
               (input_data[1] & (1 << 0)) ? "H" : "L",
               (input_data[1] & (1 << 1)) ? "H" : "L");
    } else {
        printf("当前模式: 输出模式\n");
        // 添加输出寄存器读取
        printf("读取输出寄存器值:\n");
        
        int output_data[2];
        if (ioctl(fd, IOCTL_GET_INPUT, output_data) < 0) {
            perror("读取输出寄存器失败");
        } else {
            printf("SET1 输出: 0x%02X, SET2 输出: 0x%02X\n", 
                   output_data[0], output_data[1]);
        }
    }
}

// 打印菜单
void print_menu() {
    printf("\n===== GPIO控制菜单 =====\n");
    printf("1. 切换到输入模式\n");
    printf("2. 切换到输出模式\n");
    printf("3. 所有输出高电平\n");
    printf("4. 所有输出低电平\n");
    printf("5. 设置单个GPIO输出\n");
    printf("6. 显示GPIO状态\n");
    printf("0. 退出\n");
    printf("请选择: ");
    fflush(stdout);
}

// 获取单个字符输入(不需要回车)
int getch() {
    struct termios oldt, newt;
    int ch;
    
    tcgetattr(STDIN_FILENO, &oldt);
    newt = oldt;
    newt.c_lflag &= ~(ICANON | ECHO);
    tcsetattr(STDIN_FILENO, TCSANOW, &newt);
    
    ch = getchar();
    
    tcsetattr(STDIN_FILENO, TCSANOW, &oldt);
    return ch;
}
