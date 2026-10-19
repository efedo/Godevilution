#pragma once

/**
 * @file engine/math/circle.hpp
 *
 * Interface for circle geometry.
 */


#include "engine/math/displacement.hpp"
#include "engine/math/point.hpp"

namespace devilution {

struct Circle {
	Point position;
	int radius;

	constexpr bool contains(Point point) const
	{
		Displacement diff = point - position;
		int x = diff.deltaX;
		int y = diff.deltaY;
		return x * x + y * y < radius * radius;
	}
};

} // namespace devilution
