#pragma once

/**
 * @file network/storm/storm_svid.h
 *
 * Interface for Storm video API compatibility.
 */


namespace devilution {

bool SVidPlayBegin(const char *filename, int flags);
bool SVidPlayContinue();
void SVidPlayEnd();
void SVidMute();
void SVidUnmute();

} // namespace devilution
