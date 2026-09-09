include(GoogleTest)
include(functions/copy_files)

add_library(libdevilutionx_so SHARED)
set_target_properties(libdevilutionx_so PROPERTIES RUNTIME_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR})

target_link_dependencies(libdevilutionx_so PUBLIC libdevilutionx)
set_target_properties(libdevilutionx_so PROPERTIES WINDOWS_EXPORT_ALL_SYMBOLS ON)

add_library(test_main OBJECT test/main.cpp)
target_link_dependencies(test_main PUBLIC libdevilutionx_so GTest::gtest GTest::gmock)

set(tests
  animationinfo_test
  fatal_error_test
  automap_test
  cursor_test
  debug_console_history_test
  debug_command_registry_test
  debug_overlay_state_test
  game_event_bus_test
  command_delivery_test
  replay_test
  monster_corpses_test
  diablo_test
  level_common_test
  level_l1_test
  level_l2_test
  level_l3_test
  level_l4_test
  effects_test
  inventory_test
  items_test
  math_test
  missiles_test
  monster_test
  multi_logging_test
  objects_test
  pack_test
  level_micros_test
  phase4a_lighting_test
  phase4b_entity_test
  player_test
  portal_test
  quests_test
  world_renderer_test
  stores_test
  spells_test
  tile_properties_test
  timedemo_test
  tile_migration_sync_test
  townerdat_test
  writehero_test
  vendor_test
  panel_state_test
  store_transaction_test
  visual_store_test
  stash_test
  inventory_ui_test
  spell_ui_test
  char_panel_test
  menu_framework_test
)
if(NOT NONET AND NOT DISABLE_TCP)
  list(APPEND tests authoritative_envelope_codec_test)
endif()
if(DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT)
  list(APPEND tests
    server_backed_client_test
    server_backed_configuration_test
    server_backed_compatibility_test
    server_backed_player_snapshot_test
    server_backed_session_test
    server_backed_stash_test
    server_backed_vendor_command_test
    server_backed_vendor_snapshot_test
    server_backed_vendor_state_test
    server_backed_vendor_ui_test)
endif()
set(standalone_tests
  content_manifest_test
  codec_test
  crawl_test
  entity_pool_test
  mpq_file_test
  file_util_test
  format_int_test
  ini_test
  palette_blending_test
  parse_int_test
  path_test
  vision_test
  random_test
  rectangle_test
  static_vector_test
  str_cat_test
  utf8_test
)
if(NOT USE_SDL1)
  list(APPEND standalone_tests text_render_integration_test)
endif()
set(benchmarks
  clx_render_benchmark
  crawl_benchmark
  dun_render_benchmark
  light_render_benchmark
  palette_blending_benchmark
  path_benchmark
  phase4a_lighting_benchmark
)

include(test/Fixtures.cmake)

foreach(test_target ${tests} ${standalone_tests} ${benchmarks})
  add_executable(${test_target} "test/${test_target}.cpp")
  set_target_properties(${test_target} PROPERTIES RUNTIME_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR})
  if(MSVC AND test_target STREQUAL replay_test)
    target_compile_options(${test_target} PRIVATE /bigobj)
  endif()
  if(GPERF)
    target_link_libraries(${test_target} PUBLIC ${GPERFTOOLS_LIBRARIES})
  endif()
endforeach()

if(DEVILUTIONX_ENABLE_SERVER_BACKED_CLIENT)
  foreach(server_backed_test server_backed_client_test server_backed_configuration_test server_backed_compatibility_test server_backed_player_snapshot_test server_backed_session_test server_backed_stash_test server_backed_vendor_command_test server_backed_vendor_snapshot_test server_backed_vendor_state_test server_backed_vendor_ui_test)
    if(WIN32)
      target_compile_definitions(${server_backed_test} PRIVATE "DVL_PROTOBUF_EXPORT=__declspec(dllimport)")
    else()
      target_compile_definitions(${server_backed_test} PRIVATE DVL_PROTOBUF_EXPORT=)
    endif()
  endforeach()
endif()

foreach(test_target ${tests} ${standalone_tests})
  gtest_discover_tests(${test_target})
endforeach()

foreach(test_target ${tests})
  target_link_libraries(${test_target} PRIVATE test_main)
endforeach()

foreach(test_target ${standalone_tests})
  target_link_libraries(${test_target} PRIVATE GTest::gtest_main)
  target_include_directories(${test_target} PRIVATE "${PROJECT_SOURCE_DIR}/Source")
endforeach()

foreach(target ${benchmarks})
  target_link_libraries(${target} PRIVATE benchmark::benchmark benchmark::benchmark_main)
  target_include_directories(${target} PRIVATE "${PROJECT_SOURCE_DIR}/Source")
endforeach()

add_library(app_fatal_for_testing OBJECT test/app_fatal_for_testing.cpp)
target_sources(app_fatal_for_testing INTERFACE $<TARGET_OBJECTS:app_fatal_for_testing>)

add_library(language_for_testing OBJECT test/language_for_testing.cpp)
target_sources(language_for_testing INTERFACE $<TARGET_OBJECTS:language_for_testing>)

target_link_dependencies(codec_test PRIVATE libdevilutionx_codec app_fatal_for_testing)
target_sources(content_manifest_test PRIVATE Source/data/content_manifest.cpp)
target_include_directories(content_manifest_test PRIVATE 3rdParty/PicoSHA2)
target_link_dependencies(clx_render_benchmark
  PRIVATE
  DevilutionX::SDL
  tl
  app_fatal_for_testing
  language_for_testing
  libdevilutionx_clx_render
  libdevilutionx_load_clx
  libdevilutionx_log
  libdevilutionx_surface
)
target_link_dependencies(crawl_test PRIVATE libdevilutionx_crawl)
target_link_dependencies(crawl_benchmark PRIVATE libdevilutionx_crawl)
target_link_dependencies(mpq_file_test PRIVATE libdevilutionx_txtdata app_fatal_for_testing language_for_testing)
target_link_dependencies(debug_console_history_test PRIVATE libdevilutionx_debug_console_history)
target_link_dependencies(debug_overlay_state_test PRIVATE libdevilutionx_debug_overlay_state)
target_link_dependencies(dun_render_benchmark PRIVATE libdevilutionx_so)
target_link_dependencies(file_util_test PRIVATE libdevilutionx_file_util app_fatal_for_testing)
target_link_dependencies(format_int_test PRIVATE libdevilutionx_format_int language_for_testing)
target_link_dependencies(ini_test PRIVATE libdevilutionx_ini app_fatal_for_testing)
target_link_dependencies(light_render_benchmark PRIVATE libdevilutionx_light_render DevilutionX::SDL libdevilutionx_surface libdevilutionx_paths app_fatal_for_testing)
target_link_dependencies(palette_blending_test PRIVATE libdevilutionx_palette_blending DevilutionX::SDL libdevilutionx_strings GTest::gmock app_fatal_for_testing)
target_link_dependencies(palette_blending_benchmark
  PRIVATE
  DevilutionX::SDL
  libdevilutionx_palette_blending
  libdevilutionx_palette_kd_tree
  app_fatal_for_testing
)
target_link_dependencies(parse_int_test PRIVATE libdevilutionx_parse_int)
target_link_dependencies(path_test PRIVATE libdevilutionx_pathfinding libdevilutionx_direction app_fatal_for_testing)
target_link_dependencies(vision_test PRIVATE libdevilutionx_vision)
target_link_dependencies(path_benchmark PRIVATE libdevilutionx_pathfinding app_fatal_for_testing)
target_link_dependencies(phase4a_lighting_benchmark PRIVATE DevilutionX::SDL tl libdevilutionx_so)
target_link_dependencies(random_test PRIVATE libdevilutionx_random)
target_link_dependencies(static_vector_test PRIVATE libdevilutionx_random app_fatal_for_testing)
target_link_dependencies(str_cat_test PRIVATE libdevilutionx_strings)
if(DEVILUTIONX_SCREENSHOT_FORMAT STREQUAL DEVILUTIONX_SCREENSHOT_FORMAT_PNG AND NOT USE_SDL1)
  target_link_dependencies(text_render_integration_test
    PRIVATE
    DevilutionX::SDL
    GTest::gmock
    GTest::gtest
    fmt::fmt
    tl
    app_fatal_for_testing
    language_for_testing
    libdevilutionx_primitive_render
    libdevilutionx_strings
    libdevilutionx_surface
    libdevilutionx_surface_to_png
    libdevilutionx_text_render
  )
  copy_files(
    FILES
      basic-colors.png
      basic.png
      horizontal_overflow.png
      horizontal_overflow-colors.png
      kerning_fit_spacing-colors.png
      kerning_fit_spacing.png
      kerning_fit_spacing__align_center-colors.png
      kerning_fit_spacing__align_center.png
      kerning_fit_spacing__align_center__newlines.png
      kerning_fit_spacing__align_center__newlines_in_fmt-colors.png
      kerning_fit_spacing__align_center__newlines_in_value-colors.png
      kerning_fit_spacing__align_right-colors.png
      kerning_fit_spacing__align_right.png
      vertical_overflow.png
      vertical_overflow-colors.png
      cursor-start.png
      cursor-middle.png
      cursor-end.png
      multiline_cursor-end_first_line.png
      multiline_cursor-start_second_line.png
      multiline_cursor-middle_second_line.png
      multiline_cursor-end_second_line.png
      highlight-partial.png
      highlight-full.png
      multiline_highlight.png
    SRC_PREFIX test/fixtures/text_render_integration_test/
    OUTPUT_DIR "${DEVILUTIONX_TEST_FIXTURES_OUTPUT_DIRECTORY}/text_render_integration_test"
    OUTPUT_VARIABLE _text_render_integration_test_fixtures
  )
  add_custom_target(text_render_integration_test_resources
    DEPENDS
    "${DEVILUTIONX_ASSETS_OUTPUT_DIRECTORY}/fonts/12-00.clx"
    "${DEVILUTIONX_ASSETS_OUTPUT_DIRECTORY}/fonts/goldui.trn"
    "${DEVILUTIONX_ASSETS_OUTPUT_DIRECTORY}/fonts/golduis.trn"
    "${DEVILUTIONX_ASSETS_OUTPUT_DIRECTORY}/fonts/grayuis.trn"
    "${DEVILUTIONX_ASSETS_OUTPUT_DIRECTORY}/fonts/grayui.trn"
    "${DEVILUTIONX_ASSETS_OUTPUT_DIRECTORY}/ui_art/diablo.pal"
    ${_text_render_integration_test_fixtures}
  )
  add_dependencies(text_render_integration_test text_render_integration_test_resources)
endif()
target_link_dependencies(utf8_test PRIVATE libdevilutionx_utf8)

target_include_directories(writehero_test PRIVATE 3rdParty/PicoSHA2)
