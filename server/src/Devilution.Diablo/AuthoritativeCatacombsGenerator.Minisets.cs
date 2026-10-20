namespace Devilution.Diablo;

/// <summary>
/// Exact Catacombs level-2 miniset search/replacement tables from the native generator.
/// </summary>
public static partial class AuthoritativeCatacombsGenerator
{
	private static readonly byte[] BaseTypes =
	[
		0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 0, 0, 0, 0, 0, 0,
		0, 17, 18, 1, 1, 2, 2, 1, 1, 1, 1, 1, 1, 2, 2, 2,
		2, 2, 0, 0, 0, 0, 8, 0, 0, 0, 0, 0, 0, 0, 0, 0,
		0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
		0, 0, 0, 0, 1, 1, 1, 0, 0, 2, 2, 2, 0, 0, 0, 1,
		0, 0, 0, 0, 0, 0, 0, 0, 3, 3, 3, 3, 0, 0, 0, 0,
		0, 0, 0, 0, 0, 0, 0, 0, 0, 3, 3, 3, 0, 3, 0, 3,
		0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
		0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
		0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
		0,
	];
	private static readonly byte[] ShadowTypes =
	[
		0, 1, 2, 3, 0, 0, 6, 0, 0, 9, 0, 0, 0, 0, 0, 0,
		0, 0, 0, 1, 1, 2, 2, 1, 1, 1, 1, 1, 1, 2, 2, 2,
		2, 2, 0, 0, 0, 0, 0, 6, 6, 6, 9, 0, 0, 0, 0, 0,
		0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
		0, 0, 0, 0, 1, 1, 1, 0, 0, 2, 2, 2, 0, 0, 0, 1,
		1, 1, 1, 6, 2, 2, 2, 0, 3, 3, 3, 3, 0, 0, 0, 0,
		0, 0, 0, 0, 0, 0, 0, 0, 3, 3, 3, 3, 3, 3, 3, 3,
		3, 3, 3, 3, 1, 1, 2, 2, 3, 3, 3, 3, 1, 1, 2, 2,
		3, 3, 3, 3, 1, 1, 3, 3, 2, 2, 3, 3, 0, 0, 0, 0,
		0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
		0,
	];
	private static readonly int[][] Patterns =
	[
		[0, 0, 0, 0, 0, 0, 0, 0, 0, 3],
		[0, 0, 0, 0, 2, 0, 0, 0, 0, 3],
		[0, 7, 0, 0, 1, 0, 0, 5, 0, 2],
		[0, 5, 0, 0, 1, 0, 0, 7, 0, 2],
		[0, 0, 0, 7, 1, 5, 0, 0, 0, 1],
		[0, 0, 0, 5, 1, 7, 0, 0, 0, 1],
		[0, 1, 0, 0, 3, 0, 0, 1, 0, 4],
		[0, 0, 0, 1, 3, 1, 0, 0, 0, 5],
		[0, 6, 0, 6, 1, 0, 0, 0, 0, 6],
		[0, 6, 0, 0, 1, 6, 0, 0, 0, 9],
		[0, 0, 0, 6, 1, 0, 0, 6, 0, 7],
		[0, 0, 0, 0, 1, 6, 0, 6, 0, 8],
		[0, 6, 0, 6, 6, 0, 8, 6, 0, 7],
		[0, 6, 8, 6, 6, 6, 0, 0, 0, 9],
		[0, 6, 0, 0, 6, 6, 0, 6, 8, 8],
		[6, 6, 6, 6, 6, 6, 0, 6, 0, 8],
		[2, 6, 6, 6, 6, 6, 0, 6, 0, 8],
		[7, 7, 7, 6, 6, 6, 0, 6, 0, 8],
		[6, 6, 2, 6, 6, 6, 0, 6, 0, 8],
		[6, 2, 6, 6, 6, 6, 0, 6, 0, 8],
		[2, 6, 6, 6, 6, 6, 0, 6, 0, 8],
		[6, 7, 7, 6, 6, 6, 0, 6, 0, 8],
		[4, 4, 6, 6, 6, 6, 2, 6, 2, 8],
		[2, 2, 2, 2, 6, 2, 2, 6, 2, 7],
		[2, 2, 2, 2, 6, 2, 6, 6, 6, 7],
		[2, 2, 6, 2, 6, 6, 2, 2, 6, 9],
		[2, 6, 2, 2, 6, 2, 2, 2, 2, 6],
		[2, 2, 2, 2, 6, 6, 2, 2, 2, 9],
		[2, 2, 2, 6, 6, 2, 2, 2, 2, 6],
		[2, 2, 0, 2, 6, 6, 2, 2, 0, 9],
		[0, 0, 0, 0, 4, 0, 0, 0, 0, 12],
		[0, 1, 0, 0, 1, 4, 0, 1, 0, 10],
		[0, 0, 0, 1, 1, 1, 0, 4, 0, 11],
		[0, 0, 0, 6, 1, 4, 0, 1, 0, 14],
		[0, 6, 0, 1, 1, 0, 0, 4, 0, 16],
		[0, 6, 0, 0, 1, 1, 0, 4, 0, 15],
		[0, 0, 0, 0, 1, 1, 0, 1, 4, 13],
		[8, 8, 8, 8, 1, 1, 0, 1, 1, 13],
		[8, 8, 4, 8, 1, 1, 0, 1, 1, 10],
		[0, 0, 0, 1, 1, 1, 1, 1, 1, 11],
		[1, 1, 1, 1, 1, 1, 2, 2, 8, 2],
		[0, 1, 0, 1, 1, 4, 1, 1, 0, 16],
		[0, 0, 0, 1, 1, 1, 1, 1, 4, 11],
		[1, 1, 4, 1, 1, 1, 0, 2, 2, 2],
		[1, 1, 1, 1, 1, 1, 6, 2, 6, 2],
		[4, 1, 1, 1, 1, 1, 6, 2, 6, 2],
		[2, 2, 2, 1, 1, 1, 4, 1, 1, 11],
		[4, 1, 1, 1, 1, 1, 2, 2, 2, 2],
		[1, 1, 4, 1, 1, 1, 2, 2, 1, 2],
		[4, 1, 1, 1, 1, 1, 1, 2, 2, 2],
		[2, 2, 6, 1, 1, 1, 4, 1, 1, 11],
		[4, 1, 1, 1, 1, 1, 2, 2, 6, 2],
		[1, 2, 2, 1, 1, 1, 4, 1, 1, 11],
		[0, 1, 1, 0, 1, 1, 0, 1, 1, 10],
		[2, 1, 1, 3, 1, 1, 2, 1, 1, 14],
		[1, 1, 0, 1, 1, 2, 1, 1, 0, 1],
		[0, 4, 0, 1, 1, 1, 0, 1, 1, 14],
		[4, 1, 0, 1, 1, 0, 1, 1, 0, 1],
		[0, 1, 0, 4, 1, 1, 0, 1, 1, 15],
		[1, 1, 1, 1, 1, 1, 0, 2, 2, 2],
		[0, 1, 1, 2, 1, 1, 2, 1, 4, 10],
		[2, 1, 1, 1, 1, 1, 0, 4, 0, 16],
		[1, 1, 4, 1, 1, 2, 0, 1, 2, 1],
		[2, 1, 1, 2, 1, 1, 1, 1, 4, 10],
		[1, 1, 2, 1, 1, 2, 4, 1, 8, 1],
		[2, 1, 4, 1, 1, 1, 4, 4, 1, 16],
		[2, 1, 1, 1, 1, 1, 1, 1, 1, 16],
		[1, 1, 2, 1, 1, 1, 1, 1, 1, 15],
		[1, 1, 1, 1, 1, 1, 2, 1, 1, 14],
		[4, 1, 1, 1, 1, 1, 2, 1, 1, 14],
		[1, 1, 1, 1, 1, 1, 1, 1, 2, 8],
		[0, 0, 0, 0, 255, 0, 0, 0, 0, 0],
	];

	private static readonly MiniSet VARCH1 = new(
		2, 4,
		[
			3, 0,
			3, 1,
			3, 4,
			0, 7,
		],
		[
			48, 0,
			51, 39,
			47, 44,
			0, 0,
		]
	);
	private static readonly MiniSet VARCH2 = new(
		2, 4,
		[
			3, 0,
			3, 1,
			3, 4,
			0, 8,
		],
		[
			48, 0,
			51, 39,
			47, 44,
			0, 0,
		]
	);
	private static readonly MiniSet VARCH3 = new(
		2, 4,
		[
			3, 0,
			3, 1,
			3, 4,
			0, 6,
		],
		[
			48, 0,
			51, 39,
			47, 44,
			0, 0,
		]
	);
	private static readonly MiniSet VARCH4 = new(
		2, 4,
		[
			3, 0,
			3, 1,
			3, 4,
			0, 9,
		],
		[
			48, 0,
			51, 39,
			47, 44,
			0, 0,
		]
	);
	private static readonly MiniSet VARCH5 = new(
		2, 4,
		[
			3, 0,
			3, 1,
			3, 4,
			0, 14,
		],
		[
			48, 0,
			51, 39,
			47, 44,
			0, 0,
		]
	);
	private static readonly MiniSet VARCH6 = new(
		2, 4,
		[
			3, 0,
			3, 1,
			3, 4,
			0, 13,
		],
		[
			48, 0,
			51, 39,
			47, 44,
			0, 0,
		]
	);
	private static readonly MiniSet VARCH7 = new(
		2, 4,
		[
			3, 0,
			3, 1,
			3, 4,
			0, 16,
		],
		[
			48, 0,
			51, 39,
			47, 44,
			0, 0,
		]
	);
	private static readonly MiniSet VARCH8 = new(
		2, 4,
		[
			3, 0,
			3, 1,
			3, 4,
			0, 15,
		],
		[
			48, 0,
			51, 39,
			47, 44,
			0, 0,
		]
	);
	private static readonly MiniSet VARCH9 = new(
		2, 4,
		[
			3, 0,
			3, 8,
			3, 4,
			0, 7,
		],
		[
			48, 0,
			51, 42,
			47, 44,
			0, 0,
		]
	);
	private static readonly MiniSet VARCH10 = new(
		2, 4,
		[
			3, 0,
			3, 8,
			3, 4,
			0, 8,
		],
		[
			48, 0,
			51, 42,
			47, 44,
			0, 0,
		]
	);
	private static readonly MiniSet VARCH11 = new(
		2, 4,
		[
			3, 0,
			3, 8,
			3, 4,
			0, 6,
		],
		[
			48, 0,
			51, 42,
			47, 44,
			0, 0,
		]
	);
	private static readonly MiniSet VARCH12 = new(
		2, 4,
		[
			3, 0,
			3, 8,
			3, 4,
			0, 9,
		],
		[
			48, 0,
			51, 42,
			47, 44,
			0, 0,
		]
	);
	private static readonly MiniSet VARCH13 = new(
		2, 4,
		[
			3, 0,
			3, 8,
			3, 4,
			0, 14,
		],
		[
			48, 0,
			51, 42,
			47, 44,
			0, 0,
		]
	);
	private static readonly MiniSet VARCH14 = new(
		2, 4,
		[
			3, 0,
			3, 8,
			3, 4,
			0, 13,
		],
		[
			48, 0,
			51, 42,
			47, 44,
			0, 0,
		]
	);
	private static readonly MiniSet VARCH15 = new(
		2, 4,
		[
			3, 0,
			3, 8,
			3, 4,
			0, 16,
		],
		[
			48, 0,
			51, 42,
			47, 44,
			0, 0,
		]
	);
	private static readonly MiniSet VARCH16 = new(
		2, 4,
		[
			3, 0,
			3, 8,
			3, 4,
			0, 15,
		],
		[
			48, 0,
			51, 42,
			47, 44,
			0, 0,
		]
	);
	private static readonly MiniSet VARCH17 = new(
		2, 3,
		[
			2, 7,
			3, 4,
			0, 7,
		],
		[
			141, 39,
			47, 44,
			0, 0,
		]
	);
	private static readonly MiniSet VARCH18 = new(
		2, 3,
		[
			2, 7,
			3, 4,
			0, 8,
		],
		[
			141, 39,
			47, 44,
			0, 0,
		]
	);
	private static readonly MiniSet VARCH19 = new(
		2, 3,
		[
			2, 7,
			3, 4,
			0, 6,
		],
		[
			141, 39,
			47, 44,
			0, 0,
		]
	);
	private static readonly MiniSet VARCH20 = new(
		2, 3,
		[
			2, 7,
			3, 4,
			0, 9,
		],
		[
			141, 39,
			47, 44,
			0, 0,
		]
	);
	private static readonly MiniSet VARCH21 = new(
		2, 3,
		[
			2, 7,
			3, 4,
			0, 14,
		],
		[
			141, 39,
			47, 44,
			0, 0,
		]
	);
	private static readonly MiniSet VARCH22 = new(
		2, 3,
		[
			2, 7,
			3, 4,
			0, 13,
		],
		[
			141, 39,
			47, 44,
			0, 0,
		]
	);
	private static readonly MiniSet VARCH23 = new(
		2, 3,
		[
			2, 7,
			3, 4,
			0, 16,
		],
		[
			141, 39,
			47, 44,
			0, 0,
		]
	);
	private static readonly MiniSet VARCH24 = new(
		2, 3,
		[
			2, 7,
			3, 4,
			0, 15,
		],
		[
			141, 39,
			47, 44,
			0, 0,
		]
	);
	private static readonly MiniSet VARCH25 = new(
		2, 4,
		[
			3, 0,
			3, 4,
			3, 1,
			0, 7,
		],
		[
			48, 0,
			51, 39,
			47, 44,
			0, 0,
		]
	);
	private static readonly MiniSet VARCH26 = new(
		2, 4,
		[
			3, 0,
			3, 4,
			3, 1,
			0, 8,
		],
		[
			48, 0,
			51, 39,
			47, 44,
			0, 0,
		]
	);
	private static readonly MiniSet VARCH27 = new(
		2, 4,
		[
			3, 0,
			3, 4,
			3, 1,
			0, 6,
		],
		[
			48, 0,
			51, 39,
			47, 44,
			0, 0,
		]
	);
	private static readonly MiniSet VARCH28 = new(
		2, 4,
		[
			3, 0,
			3, 4,
			3, 1,
			0, 9,
		],
		[
			48, 0,
			51, 39,
			47, 44,
			0, 0,
		]
	);
	private static readonly MiniSet VARCH29 = new(
		2, 4,
		[
			3, 0,
			3, 4,
			3, 1,
			0, 14,
		],
		[
			48, 0,
			51, 39,
			47, 44,
			0, 0,
		]
	);
	private static readonly MiniSet VARCH30 = new(
		2, 4,
		[
			3, 0,
			3, 4,
			3, 1,
			0, 13,
		],
		[
			48, 0,
			51, 39,
			47, 44,
			0, 0,
		]
	);
	private static readonly MiniSet VARCH31 = new(
		2, 4,
		[
			3, 0,
			3, 4,
			3, 1,
			0, 16,
		],
		[
			48, 0,
			51, 39,
			47, 44,
			0, 0,
		]
	);
	private static readonly MiniSet VARCH32 = new(
		2, 4,
		[
			3, 0,
			3, 4,
			3, 1,
			0, 15,
		],
		[
			48, 0,
			51, 39,
			47, 44,
			0, 0,
		]
	);
	private static readonly MiniSet VARCH33 = new(
		2, 4,
		[
			2, 0,
			3, 8,
			3, 4,
			0, 7,
		],
		[
			142, 0,
			51, 42,
			47, 44,
			0, 0,
		]
	);
	private static readonly MiniSet VARCH34 = new(
		2, 4,
		[
			2, 0,
			3, 8,
			3, 4,
			0, 8,
		],
		[
			142, 0,
			51, 42,
			47, 44,
			0, 0,
		]
	);
	private static readonly MiniSet VARCH35 = new(
		2, 4,
		[
			2, 0,
			3, 8,
			3, 4,
			0, 6,
		],
		[
			142, 0,
			51, 42,
			47, 44,
			0, 0,
		]
	);
	private static readonly MiniSet VARCH36 = new(
		2, 4,
		[
			2, 0,
			3, 8,
			3, 4,
			0, 9,
		],
		[
			142, 0,
			51, 42,
			47, 44,
			0, 0,
		]
	);
	private static readonly MiniSet VARCH37 = new(
		2, 4,
		[
			2, 0,
			3, 8,
			3, 4,
			0, 14,
		],
		[
			142, 0,
			51, 42,
			47, 44,
			0, 0,
		]
	);
	private static readonly MiniSet VARCH38 = new(
		2, 4,
		[
			2, 0,
			3, 8,
			3, 4,
			0, 13,
		],
		[
			142, 0,
			51, 42,
			47, 44,
			0, 0,
		]
	);
	private static readonly MiniSet VARCH39 = new(
		2, 4,
		[
			2, 0,
			3, 8,
			3, 4,
			0, 16,
		],
		[
			142, 0,
			51, 42,
			47, 44,
			0, 0,
		]
	);
	private static readonly MiniSet VARCH40 = new(
		2, 4,
		[
			2, 0,
			3, 8,
			3, 4,
			0, 15,
		],
		[
			142, 0,
			51, 42,
			47, 44,
			0, 0,
		]
	);
	private static readonly MiniSet HARCH1 = new(
		3, 2,
		[
			3, 3, 0,
			2, 5, 9,
		],
		[
			49, 46, 0,
			40, 45, 0,
		]
	);
	private static readonly MiniSet HARCH2 = new(
		3, 2,
		[
			3, 3, 0,
			2, 5, 6,
		],
		[
			49, 46, 0,
			40, 45, 0,
		]
	);
	private static readonly MiniSet HARCH3 = new(
		3, 2,
		[
			3, 3, 0,
			2, 5, 8,
		],
		[
			49, 46, 0,
			40, 45, 0,
		]
	);
	private static readonly MiniSet HARCH4 = new(
		3, 2,
		[
			3, 3, 0,
			2, 5, 7,
		],
		[
			49, 46, 0,
			40, 45, 0,
		]
	);
	private static readonly MiniSet HARCH5 = new(
		3, 2,
		[
			3, 3, 0,
			2, 5, 15,
		],
		[
			49, 46, 0,
			40, 45, 0,
		]
	);
	private static readonly MiniSet HARCH6 = new(
		3, 2,
		[
			3, 3, 0,
			2, 5, 16,
		],
		[
			49, 46, 0,
			40, 45, 0,
		]
	);
	private static readonly MiniSet HARCH7 = new(
		3, 2,
		[
			3, 3, 0,
			2, 5, 13,
		],
		[
			49, 46, 0,
			40, 45, 0,
		]
	);
	private static readonly MiniSet HARCH8 = new(
		3, 2,
		[
			3, 3, 0,
			2, 5, 14,
		],
		[
			49, 46, 0,
			40, 45, 0,
		]
	);
	private static readonly MiniSet HARCH9 = new(
		3, 2,
		[
			3, 3, 0,
			8, 5, 9,
		],
		[
			49, 46, 0,
			43, 45, 0,
		]
	);
	private static readonly MiniSet HARCH10 = new(
		3, 2,
		[
			3, 3, 0,
			8, 5, 6,
		],
		[
			49, 46, 0,
			43, 45, 0,
		]
	);
	private static readonly MiniSet HARCH11 = new(
		3, 2,
		[
			3, 3, 0,
			8, 5, 8,
		],
		[
			49, 46, 0,
			43, 45, 0,
		]
	);
	private static readonly MiniSet HARCH12 = new(
		3, 2,
		[
			3, 3, 0,
			8, 5, 7,
		],
		[
			49, 46, 0,
			43, 45, 0,
		]
	);
	private static readonly MiniSet HARCH13 = new(
		3, 2,
		[
			3, 3, 0,
			8, 5, 15,
		],
		[
			49, 46, 0,
			43, 45, 0,
		]
	);
	private static readonly MiniSet HARCH14 = new(
		3, 2,
		[
			3, 3, 0,
			8, 5, 16,
		],
		[
			49, 46, 0,
			43, 45, 0,
		]
	);
	private static readonly MiniSet HARCH15 = new(
		3, 2,
		[
			3, 3, 0,
			8, 5, 13,
		],
		[
			49, 46, 0,
			43, 45, 0,
		]
	);
	private static readonly MiniSet HARCH16 = new(
		3, 2,
		[
			3, 3, 0,
			8, 5, 14,
		],
		[
			49, 46, 0,
			43, 45, 0,
		]
	);
	private static readonly MiniSet HARCH17 = new(
		3, 2,
		[
			1, 3, 0,
			8, 5, 9,
		],
		[
			140, 46, 0,
			43, 45, 0,
		]
	);
	private static readonly MiniSet HARCH18 = new(
		3, 2,
		[
			1, 3, 0,
			8, 5, 6,
		],
		[
			140, 46, 0,
			43, 45, 0,
		]
	);
	private static readonly MiniSet HARCH19 = new(
		3, 2,
		[
			1, 3, 0,
			8, 5, 8,
		],
		[
			140, 46, 0,
			43, 45, 0,
		]
	);
	private static readonly MiniSet HARCH20 = new(
		3, 2,
		[
			1, 3, 0,
			8, 5, 7,
		],
		[
			140, 46, 0,
			43, 45, 0,
		]
	);
	private static readonly MiniSet HARCH21 = new(
		3, 2,
		[
			1, 3, 0,
			8, 5, 15,
		],
		[
			140, 46, 0,
			43, 45, 0,
		]
	);
	private static readonly MiniSet HARCH22 = new(
		3, 2,
		[
			1, 3, 0,
			8, 5, 16,
		],
		[
			140, 46, 0,
			43, 45, 0,
		]
	);
	private static readonly MiniSet HARCH23 = new(
		3, 2,
		[
			1, 3, 0,
			8, 5, 13,
		],
		[
			140, 46, 0,
			43, 45, 0,
		]
	);
	private static readonly MiniSet HARCH24 = new(
		3, 2,
		[
			1, 3, 0,
			8, 5, 14,
		],
		[
			140, 46, 0,
			43, 45, 0,
		]
	);
	private static readonly MiniSet HARCH25 = new(
		3, 2,
		[
			3, 3, 0,
			5, 2, 9,
		],
		[
			49, 46, 0,
			40, 45, 0,
		]
	);
	private static readonly MiniSet HARCH26 = new(
		3, 2,
		[
			3, 3, 0,
			5, 2, 6,
		],
		[
			49, 46, 0,
			40, 45, 0,
		]
	);
	private static readonly MiniSet HARCH27 = new(
		3, 2,
		[
			3, 3, 0,
			5, 2, 8,
		],
		[
			49, 46, 0,
			40, 45, 0,
		]
	);
	private static readonly MiniSet HARCH28 = new(
		3, 2,
		[
			3, 3, 0,
			5, 2, 7,
		],
		[
			49, 46, 0,
			40, 45, 0,
		]
	);
	private static readonly MiniSet HARCH29 = new(
		3, 2,
		[
			3, 3, 0,
			5, 2, 15,
		],
		[
			49, 46, 0,
			40, 45, 0,
		]
	);
	private static readonly MiniSet HARCH30 = new(
		3, 2,
		[
			3, 3, 0,
			5, 2, 16,
		],
		[
			49, 46, 0,
			40, 45, 0,
		]
	);
	private static readonly MiniSet HARCH31 = new(
		3, 2,
		[
			3, 3, 0,
			5, 2, 13,
		],
		[
			49, 46, 0,
			40, 45, 0,
		]
	);
	private static readonly MiniSet HARCH32 = new(
		3, 2,
		[
			3, 3, 0,
			5, 2, 14,
		],
		[
			49, 46, 0,
			40, 45, 0,
		]
	);
	private static readonly MiniSet HARCH33 = new(
		3, 2,
		[
			1, 3, 0,
			9, 5, 9,
		],
		[
			140, 46, 0,
			40, 45, 0,
		]
	);
	private static readonly MiniSet HARCH34 = new(
		3, 2,
		[
			1, 3, 0,
			9, 5, 6,
		],
		[
			140, 46, 0,
			40, 45, 0,
		]
	);
	private static readonly MiniSet HARCH35 = new(
		3, 2,
		[
			1, 3, 0,
			9, 5, 8,
		],
		[
			140, 46, 0,
			40, 45, 0,
		]
	);
	private static readonly MiniSet HARCH36 = new(
		3, 2,
		[
			1, 3, 0,
			9, 5, 7,
		],
		[
			140, 46, 0,
			40, 45, 0,
		]
	);
	private static readonly MiniSet HARCH37 = new(
		3, 2,
		[
			1, 3, 0,
			9, 5, 15,
		],
		[
			140, 46, 0,
			40, 45, 0,
		]
	);
	private static readonly MiniSet HARCH38 = new(
		3, 2,
		[
			1, 3, 0,
			9, 5, 16,
		],
		[
			140, 46, 0,
			40, 45, 0,
		]
	);
	private static readonly MiniSet HARCH39 = new(
		3, 2,
		[
			1, 3, 0,
			9, 5, 13,
		],
		[
			140, 46, 0,
			40, 45, 0,
		]
	);
	private static readonly MiniSet HARCH40 = new(
		3, 2,
		[
			1, 3, 0,
			9, 5, 14,
		],
		[
			140, 46, 0,
			40, 45, 0,
		]
	);
	private static readonly MiniSet USTAIRS = new(
		4, 4,
		[
			3, 3, 3, 3,
			3, 3, 3, 3,
			3, 3, 3, 3,
			3, 3, 3, 3,
		],
		[
			0, 0, 0, 0,
			0, 72, 77, 0,
			0, 76, 0, 0,
			0, 0, 0, 0,
		]
	);
	private static readonly MiniSet DSTAIRS = new(
		4, 4,
		[
			3, 3, 3, 3,
			3, 3, 3, 3,
			3, 3, 3, 3,
			3, 3, 3, 3,
		],
		[
			0, 0, 0, 0,
			0, 48, 71, 0,
			0, 50, 78, 0,
			0, 0, 0, 0,
		]
	);
	private static readonly MiniSet WARPSTAIRS = new(
		4, 4,
		[
			3, 3, 3, 3,
			3, 3, 3, 3,
			3, 3, 3, 3,
			3, 3, 3, 3,
		],
		[
			0, 0, 0, 0,
			0, 158, 160, 0,
			0, 159, 0, 0,
			0, 0, 0, 0,
		]
	);
	private static readonly MiniSet CRUSHCOL = new(
		3, 3,
		[
			3, 1, 3,
			2, 6, 3,
			3, 3, 3,
		],
		[
			0, 0, 0,
			0, 83, 0,
			0, 0, 0,
		]
	);
	private static readonly MiniSet BIG1 = new(
		2, 2,
		[
			3, 3,
			3, 3,
		],
		[
			113, 0,
			112, 0,
		]
	);
	private static readonly MiniSet BIG2 = new(
		2, 2,
		[
			3, 3,
			3, 3,
		],
		[
			114, 115,
			0, 0,
		]
	);
	private static readonly MiniSet BIG3 = new(
		1, 2,
		[
			1,
			1,
		],
		[
			117,
			116,
		]
	);
	private static readonly MiniSet BIG4 = new(
		2, 1,
		[
			2, 2,
		],
		[
			118, 119,
		]
	);
	private static readonly MiniSet BIG5 = new(
		2, 2,
		[
			3, 3,
			3, 3,
		],
		[
			120, 122,
			121, 123,
		]
	);
	private static readonly MiniSet BIG6 = new(
		1, 2,
		[
			1,
			1,
		],
		[
			125,
			124,
		]
	);
	private static readonly MiniSet BIG7 = new(
		2, 1,
		[
			2, 2,
		],
		[
			126, 127,
		]
	);
	private static readonly MiniSet BIG8 = new(
		2, 2,
		[
			3, 3,
			3, 3,
		],
		[
			128, 130,
			129, 131,
		]
	);
	private static readonly MiniSet BIG9 = new(
		2, 2,
		[
			1, 3,
			1, 3,
		],
		[
			133, 135,
			132, 134,
		]
	);
	private static readonly MiniSet BIG10 = new(
		2, 2,
		[
			2, 2,
			3, 3,
		],
		[
			136, 137,
			3, 3,
		]
	);
	private static readonly MiniSet PANCREAS1 = new(
		5, 3,
		[
			3, 3, 3, 3, 3,
			3, 3, 3, 3, 3,
			3, 3, 3, 3, 3,
		],
		[
			0, 0, 0, 0, 0,
			0, 0, 108, 0, 0,
			0, 0, 0, 0, 0,
		]
	);
	private static readonly MiniSet PANCREAS2 = new(
		5, 3,
		[
			3, 3, 3, 3, 3,
			3, 3, 3, 3, 3,
			3, 3, 3, 3, 3,
		],
		[
			0, 0, 0, 0, 0,
			0, 0, 110, 0, 0,
			0, 0, 0, 0, 0,
		]
	);
	private static readonly MiniSet CTRDOOR1 = new(
		3, 3,
		[
			3, 1, 3,
			0, 4, 0,
			0, 9, 0,
		],
		[
			0, 4, 0,
			0, 1, 0,
			0, 0, 0,
		]
	);
	private static readonly MiniSet CTRDOOR2 = new(
		3, 3,
		[
			3, 1, 3,
			0, 4, 0,
			0, 8, 0,
		],
		[
			0, 4, 0,
			0, 1, 0,
			0, 0, 0,
		]
	);
	private static readonly MiniSet CTRDOOR3 = new(
		3, 3,
		[
			3, 1, 3,
			0, 4, 0,
			0, 6, 0,
		],
		[
			0, 4, 0,
			0, 1, 0,
			0, 0, 0,
		]
	);
	private static readonly MiniSet CTRDOOR4 = new(
		3, 3,
		[
			3, 1, 3,
			0, 4, 0,
			0, 7, 0,
		],
		[
			0, 4, 0,
			0, 1, 0,
			0, 0, 0,
		]
	);
	private static readonly MiniSet CTRDOOR5 = new(
		3, 3,
		[
			3, 1, 3,
			0, 4, 0,
			0, 15, 0,
		],
		[
			0, 4, 0,
			0, 1, 0,
			0, 0, 0,
		]
	);
	private static readonly MiniSet CTRDOOR6 = new(
		3, 3,
		[
			3, 1, 3,
			0, 4, 0,
			0, 13, 0,
		],
		[
			0, 4, 0,
			0, 1, 0,
			0, 0, 0,
		]
	);
	private static readonly MiniSet CTRDOOR7 = new(
		3, 3,
		[
			3, 1, 3,
			0, 4, 0,
			0, 16, 0,
		],
		[
			0, 4, 0,
			0, 1, 0,
			0, 0, 0,
		]
	);
	private static readonly MiniSet CTRDOOR8 = new(
		3, 3,
		[
			3, 1, 3,
			0, 4, 0,
			0, 14, 0,
		],
		[
			0, 4, 0,
			0, 1, 0,
			0, 0, 0,
		]
	);
}
