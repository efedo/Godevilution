#pragma once

/**
 * @file platform/ctr/asio/include/errno.h
 *
 * Interface for errno.
 */


#include_next <errno.h>

#define ESHUTDOWN (__ELASTERROR + 1)
