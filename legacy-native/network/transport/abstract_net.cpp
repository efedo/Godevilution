/**
 * @file network/transport/abstract_net.cpp
 *
 * Implementation of abstract network interface.
 */


#include "network/transport/abstract_net.h"

#include "network/transport/loopback.h"
#include "utils/stubs.h"

#ifndef NONET
#include "network/transport/cdwrap.h"

#ifndef DISABLE_ZERO_TIER
#include "network/transport/base_protocol.h"
#include "network/transport/protocol_zt.h"
#endif

#ifndef DISABLE_TCP
#include "network/transport/tcp_client.h"
#endif
#endif

namespace devilution::net {

std::unique_ptr<abstract_net> abstract_net::MakeNet(provider_t provider)
{
#ifdef NONET
	return std::make_unique<loopback>();
#else
	switch (provider) {
#ifndef DISABLE_TCP
	case SELCONN_TCP:
		return std::make_unique<cdwrap>([]() {
			return std::make_unique<tcp_client>();
		});
#endif
#ifndef DISABLE_ZERO_TIER
	case SELCONN_ZT:
		return std::make_unique<cdwrap>([]() {
			return std::make_unique<base_protocol<protocol_zt>>();
		});
#endif
	case SELCONN_LOOPBACK:
		return std::make_unique<loopback>();
	default:
		ABORT();
	}
#endif
}

} // namespace devilution::net
