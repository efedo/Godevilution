#pragma once

/**
 * @file platform/switch/asio/include/errno.h
 *
 * Interface for errno.
 */


#include_next <errno.h>

#define ESHUTDOWN (__ELASTERROR + 1)
