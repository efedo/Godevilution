#pragma once

/**
 * @file network/transport/zerotier_lwip.h
 *
 * Interface for ZeroTier lwIP integration.
 */


namespace devilution {
namespace net {

void print_ip6_addr(void *x);
void zt_ip6setup();

} // namespace net
} // namespace devilution
