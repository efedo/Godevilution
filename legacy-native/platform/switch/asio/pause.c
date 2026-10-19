/**
 * @file legacy-native/platform/switch/asio/pause.c
 *
 * pause.
 */


#include <errno.h>

int pause(void)
{
	errno = ENOSYS;
	return -1;
}