/**
 * Generic entity pool container.
 *
 * Provides a memory-efficient pool for fixed-capacity entity management with:
 * - O(1) allocation/deallocation via free-list or swap-with-last
 * - Stable or unstable indices (configurable via sparse_allocation policy)
 * - STL-like iteration over active elements
 * - Binary-compatible serialization for save-file format preservation
 */
#pragma once

#include <cstddef>
#include <cstdint>
#include <iterator>
#include <stdexcept>
#include <type_traits>
#include <utility>

namespace devilution {

/**
 * Policy for sparse allocation (stable indices with free-list).
 * Element indices never change, even after deletion.
 * Requires explicit availability tracking.
 */
struct SparseAllocationPolicy {
	static constexpr bool MaintainsStableIndices = true;
};

/**
 * Policy for dense allocation (swap-with-last deletion).
 * Element indices may change when items are deleted.
 * Faster iteration and deletion, but requires index remapping.
 */
struct DenseAllocationPolicy {
	static constexpr bool MaintainsStableIndices = false;
};

/**
 * DenseEntityPool: A fixed-capacity pool for entity management.
 *
 * @tparam T Entity type to store
 * @tparam MaxCapacity Maximum number of entities
 * @tparam AllocationPolicy SparseAllocationPolicy or DenseAllocationPolicy
 *
 * The pool maintains a contiguous array of entities and tracks active items.
 * For sparse allocation, a free-list tracks available slots.
 * For dense allocation, deletion uses swap-with-last optimization.
 */
template <typename T, size_t MaxCapacity, typename AllocationPolicy = SparseAllocationPolicy>
class DenseEntityPool {
public:
	static constexpr size_t Capacity = MaxCapacity;
	static constexpr bool StableIndices = AllocationPolicy::MaintainsStableIndices;

	using value_type = T;
	using size_type = int;
	using difference_type = int;
	using reference = T &;
	using const_reference = const T &;
	using pointer = T *;
	using const_pointer = const T *;

	/**
	 * Iterator over active entities only.
	 */
	template <typename ValueType>
	class iterator_base {
	public:
		using iterator_category = std::bidirectional_iterator_tag;
		using value_type = ValueType;
		using difference_type = int32_t;
		using pointer = ValueType *;
		using reference = ValueType &;

		iterator_base() = default;

		iterator_base(pointer data, size_type *activeIndices, size_type activeCount, size_type pos)
		    : data_(data)
		    , activeIndices_(activeIndices)
		    , activeCount_(activeCount)
		    , pos_(pos)
		{
		}

		reference operator*() const { return data_[activeIndices_[pos_]]; }
		pointer operator->() const { return &data_[activeIndices_[pos_]]; }

		iterator_base &operator++()
		{
			++pos_;
			return *this;
		}

		iterator_base operator++(int)
		{
			iterator_base tmp = *this;
			++pos_;
			return tmp;
		}

		iterator_base &operator--()
		{
			--pos_;
			return *this;
		}

		iterator_base operator--(int)
		{
			iterator_base tmp = *this;
			--pos_;
			return tmp;
		}

		bool operator==(const iterator_base &other) const { return pos_ == other.pos_; }
		bool operator!=(const iterator_base &other) const { return pos_ != other.pos_; }

		size_type index() const { return activeIndices_[pos_]; }

	private:
		pointer data_ = nullptr;
		size_type *activeIndices_ = nullptr;
		size_type activeCount_ = 0;
		size_type pos_ = 0;
	};

	using iterator = iterator_base<T>;
	using const_iterator = iterator_base<const T>;

	/**
	 * Constructs an empty pool with its sparse free-list initialized.
	 *
	 * Keeping construction equivalent to clear() makes the container safe to
	 * use before a game-specific reset hook runs, while preserving the legacy
	 * aliases that explicitly call clear() at level/session boundaries.
	 */
	DenseEntityPool()
	{
		clear();
	}

	/**
	 * Allocate a new entity at the next available slot.
	 * For sparse: uses free-list.
	 * For dense: appends to active set.
	 *
	 * @return Pointer to the new entity, or nullptr if pool is full.
	 */
	T *allocate()
	{
		if constexpr (StableIndices) {
			// Sparse: use free-list
			if (nextAvailableIndex_ >= MaxCapacity) {
				return nullptr;
			}
			size_type idx = nextAvailableIndex_;
			nextAvailableIndex_ = availableIndices_[idx];
			activeIndices_[activeCount_] = idx;
			++activeCount_;
			return &elements_[idx];
		} else {
			// Dense: append to active set
			if (activeCount_ >= MaxCapacity) {
				return nullptr;
			}
			size_type idx = activeCount_;
			activeIndices_[activeCount_] = activeCount_;
			++activeCount_;
			return &elements_[idx];
		}
	}

	/**
	 * Deallocate an entity by its pool index.
	 * For sparse: returns slot to free-list.
	 * For dense: uses swap-with-last and updates indices.
	 */
	void deallocate(size_type index)
	{
		if (index >= MaxCapacity) {
			return;
		}

		if constexpr (StableIndices) {
			// Sparse: find the active slot before returning it to the free-list.
			// This keeps repeated release calls from corrupting the free-list.
			for (size_type i = 0; i < activeCount_; ++i) {
				if (activeIndices_[i] == index) {
					availableIndices_[index] = nextAvailableIndex_;
					nextAvailableIndex_ = index;
					activeIndices_[i] = activeIndices_[--activeCount_];
					return;
				}
			}
		} else {
			// Dense: move the final packed element into the released slot.
			for (size_type i = 0; i < activeCount_; ++i) {
				if (activeIndices_[i] == index) {
					const size_type lastIndex = activeIndices_[activeCount_ - 1];
					if (index != lastIndex)
						elements_[index] = std::move(elements_[lastIndex]);
					activeIndices_[i] = index;
					--activeCount_;
					return;
				}
			}
		}
	}

	/**
	 * Get reference to element by pool index (unchecked).
	 */
	reference operator[](size_type index) { return elements_[index]; }
	const_reference operator[](size_type index) const { return elements_[index]; }

	/**
	 * Get reference to active element by active position.
	 */
	reference at(size_type activePos)
	{
		if (activePos >= activeCount_) {
			throw std::out_of_range("Active position out of range");
		}
		return elements_[activeIndices_[activePos]];
	}

	const_reference at(size_type activePos) const
	{
		if (activePos >= activeCount_) {
			throw std::out_of_range("Active position out of range");
		}
		return elements_[activeIndices_[activePos]];
	}

	/**
	 * Get raw element array pointer (for legacy access).
	 */
	T *data() { return elements_; }
	const T *data() const { return elements_; }

	/**
	 * Get active indices array pointer (for legacy access).
	 */
	size_type *activeIndices() { return activeIndices_; }
	const size_type *activeIndices() const { return activeIndices_; }

	/**
	 * Get available indices array pointer (for legacy sparse access).
	 */
	size_type *availableIndices() { return availableIndices_; }
	const size_type *availableIndices() const { return availableIndices_; }

	/**
	 * Number of active elements.
	 */
	size_type activeCount() const { return activeCount_; }
	size_type &activeCountRef() { return activeCount_; }
	const size_type &activeCountRef() const { return activeCount_; }

	size_type capacity() const { return MaxCapacity; }

	bool empty() const { return activeCount_ == 0; }

	/**
	 * Iterators over active elements.
	 */
	iterator begin() { return iterator(elements_, activeIndices_, activeCount_, 0); }
	iterator end() { return iterator(elements_, activeIndices_, activeCount_, activeCount_); }

	const_iterator begin() const { return const_iterator(elements_, activeIndices_, activeCount_, 0); }
	const_iterator end() const { return const_iterator(elements_, activeIndices_, activeCount_, activeCount_); }

	const_iterator cbegin() const { return begin(); }
	const_iterator cend() const { return end(); }

	/**
	 * Clear all elements and reset the pool.
	 */
	void clear()
	{
		if constexpr (StableIndices) {
			nextAvailableIndex_ = 0;
			for (size_type i = 0; i < MaxCapacity; ++i) {
				availableIndices_[i] = i + 1;
			}
		}
		activeCount_ = 0;
	}

private:
	T elements_[MaxCapacity] = {};
	size_type activeIndices_[MaxCapacity] = {};
	size_type activeCount_ = 0;

	// For sparse allocation only
	size_type availableIndices_[MaxCapacity] = {};
	size_type nextAvailableIndex_ = 0;
};

} // namespace devilution
