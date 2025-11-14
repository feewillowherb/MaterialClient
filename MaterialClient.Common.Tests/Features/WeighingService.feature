Feature: WeighingService - Automatic Weighing Detection and Record Creation
  As a system
  I want to automatically monitor truck scale weight and create weighing records
  So that vehicles can be weighed without manual intervention

  Background:
    Given 系统已完成授权激活
    And 已初始化通用测试数据
    And the weighing configuration has offset range from -1.0 to 1.0 kg
    And the weighing configuration has stable duration of 2000 ms
    And the truck scale weight service is available
    And the plate number capture service is available
    And the vehicle photo service is available

  Scenario: Vehicle enters scale and weight becomes stable
    Given the truck scale is in OffScale state
    And the current weight is 0.5 kg
    When the weight changes to 5.0 kg
    And the weight remains stable for 2000 ms
    Then the system state should be Weighing
    And a weighing record should be created with weight 5.0 kg
    And the weighing record should have RecordType Unmatch

  Scenario: Vehicle enters scale but leaves before stable
    Given the truck scale is in OffScale state
    And the current weight is 0.5 kg
    When the weight changes to 5.0 kg
    And the weight changes back to 0.5 kg before stable duration
    Then the system state should be OffScale
    And no weighing record should be created

  Scenario: Vehicle leaves scale after weighing
    Given the truck scale is in Weighing state
    And a weighing record exists with weight 5.0 kg
    When the weight changes to 0.5 kg
    Then the system state should be OffScale

  Scenario: Plate number capture succeeds
    Given the truck scale is in OffScale state
    And the plate number capture service returns "京A12345"
    When a weighing record is created
    Then the weighing record should have plate number "京A12345"

  Scenario: Plate number capture fails
    Given the truck scale is in OffScale state
    And the plate number capture service throws an exception
    When a weighing record is created
    Then the weighing record should be created successfully
    And the weighing record should have empty plate number

  Scenario: Vehicle photo capture succeeds
    Given the truck scale is in OffScale state
    And the vehicle photo service returns 4 photos
    When a weighing record is created
    Then the weighing record should have 4 vehicle photo attachments

  Scenario: Vehicle photo capture fails
    Given the truck scale is in OffScale state
    And the vehicle photo service throws an exception
    When a weighing record is created
    Then the weighing record should be created successfully
    And the weighing record should have no vehicle photo attachments

