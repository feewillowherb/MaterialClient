Feature: WeighingMatchingService - Automatic Matching and Waybill Creation
  As a system
  I want to automatically match weighing records and create waybills
  So that complete delivery/receiving records can be generated

  Background:
    Given 系统已完成授权激活
    And 已初始化通用测试数据
    And the weighing configuration has match duration of 3 hours
    And the weighing record repository is available
    And the waybill repository is available

  Scenario: Match two records with same plate number within time window - Delivery type
    Given Weighing records as below
      | PlateNumber | Weight | CreatedAt           | ProviderId |
      | 京A12345    | 10.0   | 2025-01-01 10:00:00 |            |
      | 京A12345    | 15.0   | 2025-01-01 10:30:00 |            |
    And the delivery type is Delivery
    When matching is performed
    Then 1 waybill should be created
    And Waybills as below
      | PlateNumber | OrderTruckWeight | OrderTotalWeight | OrderGoodsWeight | JoinTime           | OutTime           | Record1MatchedType | Record2MatchedType |
      | 京A12345    | 10.0             | 15.0             | 5.0              | 2025-01-01 10:00:00 | 2025-01-01 10:30:00 | Join               | Out                |

  Scenario: Match two records with same plate number within time window - Receiving type
    Given Weighing records as below
      | PlateNumber | Weight | CreatedAt           | ProviderId |
      | 京A12345    | 15.0   | 2025-01-01 10:00:00 |            |
      | 京A12345    | 10.0   | 2025-01-01 10:30:00 |            |
    And the delivery type is Receiving
    When matching is performed
    Then 1 waybill should be created
    And Waybills as below
      | PlateNumber | Record1MatchedType | Record2MatchedType |
      | 京A12345    | Join               | Out                |

  Scenario: Match fails when weight relationship does not match - Delivery type
    Given Weighing records as below
      | PlateNumber | Weight | CreatedAt           | ProviderId |
      | 京A12345    | 15.0   | 2025-01-01 10:00:00 |            |
      | 京A12345    | 10.0   | 2025-01-01 10:30:00 |            |
    And the delivery type is Delivery
    When matching is performed
    Then 0 waybills should be created
    And record 1 should have RecordType Unmatch
    And record 2 should have RecordType Unmatch

  Scenario: Match fails when time window is exceeded
    Given Weighing records as below
      | PlateNumber | Weight | CreatedAt           | ProviderId |
      | 京A12345    | 10.0   | 2025-01-01 10:00:00 |            |
      | 京A12345    | 15.0   | 2025-01-01 14:00:00 |            |
    And the delivery type is Delivery
    When matching is performed
    Then 0 waybills should be created
    And record 1 should have RecordType Unmatch
    And record 2 should have RecordType Unmatch

  Scenario: Match fails when plate numbers are different
    Given Weighing records as below
      | PlateNumber | Weight | CreatedAt           | ProviderId |
      | 京A12345    | 10.0   | 2025-01-01 10:00:00 |            |
      | 京B67890    | 15.0   | 2025-01-01 10:30:00 |            |
    And the delivery type is Delivery
    When matching is performed
    Then 0 waybills should be created
    And record 1 should have RecordType Unmatch
    And record 2 should have RecordType Unmatch

  Scenario: Select shortest time interval when multiple candidates exist
    Given Weighing records as below
      | PlateNumber | Weight | CreatedAt           | ProviderId |
      | 京A12345    | 10.0   | 2025-01-01 10:00:00 |            |
      | 京A12345    | 15.0   | 2025-01-01 10:30:00 |            |
      | 京A12345    | 20.0   | 2025-01-01 11:00:00 |            |
    And the delivery type is Delivery
    When matching is performed
    Then 1 waybill should be created
    And record 1 should have RecordType Join
    And record 2 should have RecordType Out
    And record 3 should have RecordType Unmatch

  Scenario: Extract Provider from Join or Out record
    Given Weighing records as below
      | PlateNumber | Weight | CreatedAt | ProviderId |
      | 京A12345    | 10.0   | 2025-01-01 10:00:00 | 1          |
      | 京A12345    | 15.0   | 2025-01-01 10:30:00 |            |
    And the delivery type is Delivery
    When matching is performed
    Then 1 waybill should be created
    And Waybills as below
      | PlateNumber | ProviderId |
      | 京A12345    | 1         |
