obj-m := f75111_gpio.o

KDIR := /lib/modules/$(shell uname -r)/build
PWD := $(shell pwd)

all:
	$(MAKE) -C $(KDIR) M=$(PWD) modules

clean:
	$(MAKE) -C $(KDIR) M=$(PWD) clean

install: all
	-sudo rmmod f75111_gpio 2>/dev/null
	sudo insmod f75111_gpio.ko
	sudo rm -f /dev/f75111_gpio
	sudo mknod /dev/f75111_gpio c $(shell awk '$$2=="f75111_gpio" {print $$1}' /proc/devices) 0
	sudo chmod 666 /dev/f75111_gpio
	@echo "模块已安装，设备节点已创建"

uninstall:
	-sudo rmmod f75111_gpio 2>/dev/null
	sudo rm -f /dev/f75111_gpio
	@echo "模块已卸载"
