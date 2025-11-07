Feature: WeighingMatchingService - Automatic Matching and Waybill Creation
  As a system
  I want to automatically match weighing records and create waybills
  So that complete delivery/receiving records can be generated

  Background:
    Given the weighing configuration has match duration of 3 hours
    And the weighing record repository is available
    And the waybill repository is available

  Scenario: Match two records with same plate number within time window - Delivery type
    Given there are 2 unmatched weighing records
    And record 1 has plate number "京A12345" and weight 10.0 kg created at "2025-01-01 10:00:00"
    And record 2 has plate number "京A12345" and weight 15.0 kg created at "2025-01-01 10:30:00"
    And the delivery type is Delivery
    When matching is performed
    Then 1 waybill should be created
    And record 1 should have RecordType Join
    And record 2 should have RecordType Out
    And the waybill should have OrderNo generated from Guid
    And the waybill should have plate number "京A12345"
    And the waybill should have JoinTime "2025-01-01 10:00:00"
    And the waybill should have OutTime "2025-01-01 10:30:00"
    And the waybill should have OrderTruckWeight 10.0 kg
    And the waybill should have OrderTotalWeight 15.0 kg
    And the waybill should have OrderGoodsWeight 5.0 kg

  Scenario: Match two records with same plate number within time window - Receiving type
    Given there are 2 unmatched weighing records
    And record 1 has plate number "京A12345" and weight 15.0 kg created at "2025-01-01 10:00:00"
    And record 2 has plate number "京A12345" and weight 10.0 kg created at "2025-01-01 10:30:00"
    And the delivery type is Receiving
    When matching is performed
    Then 1 waybill should be created
    And record 1 should have RecordType Join
    And record 2 should have RecordType Out

  Scenario: Match fails when weight relationship does not match - Delivery type
    Given there are 2 unmatched weighing records
    And record 1 has plate number "京A12345" and weight 15.0 kg created at "2025-01-01 10:00:00"
    And record 2 has plate number "京A12345" and weight 10.0 kg created at "2025-01-01 10:30:00"
    And the delivery type is Delivery
    When matching is performed
    Then 0 waybills should be created
    And record 1 should have RecordType Unmatch
    And record 2 should have RecordType Unmatch

  Scenario: Match fails when time window is exceeded
    Given there are 2 unmatched weighing records
    And record 1 has plate number "京A12345" and weight 10.0 kg created at "2025-01-01 10:00:00"
    And record 2 has plate number "京A12345" and weight 15.0 kg created at "2025-01-01 14:00:00"
    And the delivery type is Delivery
    When matching is performed
    Then 0 waybills should be created
    And record 1 should have RecordType Unmatch
    And record 2 should have RecordType Unmatch

  Scenario: Match fails when plate numbers are different
    Given there are 2 unmatched weighing records
    And record 1 has plate number "京A12345" and weight 10.0 kg created at "2025-01-01 10:00:00"
    And record 2 has plate number "京B67890" and weight 15.0 kg created at "2025-01-01 10:30:00"
    And the delivery type is Delivery
    When matching is performed
    Then 0 waybills should be created
    And record 1 should have RecordType Unmatch
    And record 2 should have RecordType Unmatch

  Scenario: Select shortest time interval when multiple candidates exist
    Given there are 3 unmatched weighing records with same plate number "京A12345"
    And record 1 has weight 10.0 kg created at "2025-01-01 10:00:00"
    And record 2 has weight 15.0 kg created at "2025-01-01 10:30:00"
    And record 3 has weight 20.0 kg created at "2025-01-01 11:00:00"
    And the delivery type is Delivery
    When matching is performed
    Then 1 waybill should be created
    And record 1 should have RecordType Join
    And record 2 should have RecordType Out
    And record 3 should have RecordType Unmatch

  Scenario: Extract Provider from Join or Out record
    Given there are 2 unmatched weighing records
    And record 1 has plate number "京A12345" and weight 10.0 kg and ProviderId 1
    And record 2 has plate number "京A12345" and weight 15.0 kg and ProviderId null
    And the delivery type is Delivery
    When matching is performed
    Then 1 waybill should be created
    And the waybill should have ProviderId 1

  # Note: Waybill does not have MaterialId property, so this scenario is removed

