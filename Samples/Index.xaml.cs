using FdSoft.Common.Ext;
using FdSoft.Material.Lib;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Windows;
using System.Windows.Controls;

namespace FdSoft.Material.WpfClient.Order
{
    /// <summary>
    /// Index.xaml 的交互逻辑
    /// </summary>
    public partial class Index : Window
    {
        bProvider objClass = new bProvider();
        int PageSize, CurPage, RecordCount;
        int deliveryType;

        public Index()
        {
            InitializeComponent();
            InitDataGrid();
        }

        public void Btn_Head_QueryShouLiao_Click(object sender, RoutedEventArgs e)
        {
            InitDataGrid();
        }

        private void List()
        {
            try
            {
                var tables = $@"Material_Order a 
left join Material_OrderGoods b on b.OrderId=a.OrderId 
left join Material_Goods e on e.GoodsId=b.GoodsId 
left join Material_GoodsUnits c on c.UnitId=b.UnitId
left join Material_Provider d on d.ProviderId=a.ProviderId";
                var cols = $@"a.OrderNo,a.TruckNo,a.DispatchNo,a.Remark,a.OrderType,a.JoinTime,a.OutTime,a.DeliveryType,
b.GoodsTakeWeight,b.GoodsPlanOnPcs,b.GoodsPlanOnWeight,b.GoodsPcs,b.GoodsWeight,
e.GoodsName,e.Size,e.UpperLimit*100 UpperLimit,e.LowerLimit*100 LowerLimit,
c.UnitName,c.Rate,d.ProviderName";
                var orderby = $@"a.AddTime, a.OrderNo desc";
                var where = $@" and a.deletestatus=0";

                var sqlParams = new List<SqliteParameter>();
                if (!string.IsNullOrEmpty(txt_GetTruckNo.Text))
                {
                    sqlParams.Add(new SqliteParameter("@TruckNo", "%" + txt_GetTruckNo.Text + "%"));
                    where += " and TruckNo like @TruckNo";
                }
                if (!string.IsNullOrEmpty(txt_GetGoodsName.Text))
                {
                    sqlParams.Add(new SqliteParameter("@GoodsName", "%" + txt_GetGoodsName.Text + "%"));
                    where += " and GoodsName like @GoodsName";
                }
                var cbControl = (ContentControl)this.cb_Head_OrderType.SelectedItem;
                if (this.cb_Head_OrderType.SelectedIndex > 0 && Convert.ToInt32(cbControl.Tag) > 0)
                {
                    sqlParams.Add(new SqliteParameter("@OrderType", cbControl.Tag));
                    where += " and OrderType=@OrderType";
                }

                //where += " and DeliveryType=" + deliveryType;
                if (!string.IsNullOrEmpty(txt_GetOrderNo.Text))
                {
                    sqlParams.Add(new SqliteParameter("@OrderNo", "%" + txt_GetOrderNo.Text + "%"));
                    where += " and OrderNo like @OrderNo";
                }
                if (dp_Head_JoinDateStart.SelectedDate.HasValue)
                {
                    sqlParams.Add(new SqliteParameter("@JoinDateStart", dp_Head_JoinDateStart.SelectedDate));
                    where += " and JoinTime>=@JoinDateStart";
                }
                if (dp_Head_JoinDateEnd.SelectedDate.HasValue)
                {
                    sqlParams.Add(new SqliteParameter("@JoinDateEnd", dp_Head_JoinDateEnd.SelectedDate));
                    where += " and JoinTime<=@JoinDateEnd";
                }

                var dt = objClass.GetPagedListDt(PageSize, CurPage, out RecordCount, orderby, null, "", cols, tables, where, null, sqlParams.ToArray());
                #region 计算新列
                if (dt != null)
                {
                    dt.Columns.Add(new DataColumn("GoodsNameSize", typeof(string)));
                    dt.Columns.Add(new DataColumn("UpperLowerLimit", typeof(string)));
                    dt.Columns.Add(new DataColumn("UnitNameRate", typeof(string)));
                    dt.Columns.Add(new DataColumn("OrderTypeName", typeof(string)));
                    dt.Columns.Add(new DataColumn("GoodsPlanOnPcsDesc", typeof(string)));
                    dt.Columns.Add(new DataColumn("GoodsPlanOnWeightDesc", typeof(string)));
                    dt.Columns.Add(new DataColumn("GoodsTakeWeightDesc", typeof(string)));
                    dt.Columns.Add(new DataColumn("GoodsPcsDesc", typeof(string)));
                    dt.Columns.Add(new DataColumn("GoodsWeightDesc", typeof(string)));
                    dt.Columns.Add(new DataColumn("TypeName", typeof(string)));
                    if (dt != null && dt.Rows.Count > 0)
                    {
                        foreach (DataRow dataRow in dt.Rows)
                        {
                            var isShouLiao = Convert.ToInt32(dataRow["DeliveryType"]) == 0;

                            switch (dataRow["OrderType"].ToString())
                            {
                                case "0":
                                    dataRow["OrderTypeName"] = isShouLiao ? "收货中" : "发料中";
                                    break;
                                case "1":
                                    dataRow["OrderTypeName"] = isShouLiao ? "已收货" : "已发料";
                                    break;
                                case "2":
                                    dataRow["OrderTypeName"] = "已取消";
                                    break;
                                default:
                                    dataRow["OrderTypeName"] = "未知";
                                    break;
                            }

                            dataRow["GoodsNameSize"] = dataRow["GoodsName"] + " " + dataRow["Size"];
                            dataRow["UpperLowerLimit"] = dataRow["UpperLimit"] + "%~" + dataRow["LowerLimit"] +"%";
                            if(!string.IsNullOrEmpty(dataRow["Rate"].ToString()))
                                dataRow["UnitNameRate"] = dataRow["Rate"] + " 吨/" + dataRow["UnitName"];
                            dataRow["GoodsPlanOnPcsDesc"] = dataRow["GoodsPlanOnPcs"] + " " + dataRow["UnitName"];
                            if(!string.IsNullOrEmpty(dataRow["GoodsPlanOnWeight"].ToString()))
                                dataRow["GoodsPlanOnWeightDesc"] = dataRow["GoodsPlanOnWeight"] + " 吨";
                            if(!string.IsNullOrEmpty(dataRow["GoodsTakeWeight"].ToString()))
                                dataRow["GoodsTakeWeightDesc"] = dataRow["GoodsTakeWeight"] + " 吨";
                            dataRow["GoodsPcsDesc"] = dataRow["GoodsPcs"] + " " + dataRow["UnitName"];
                            if(!string.IsNullOrEmpty(dataRow["GoodsWeight"].ToString()))
                                dataRow["GoodsWeightDesc"] = dataRow["GoodsWeight"] + " 吨";
                            dataRow["TypeName"] = isShouLiao ? "收料" : "发料";
                        }
                    }
                }
                #endregion

                if (dt != null && dt.Rows.Count > 0)
                    dg_Data_List1.ItemsSource = dt.AsDataView();
                else
                    dg_Data_List1.ItemsSource = null;

                lbl_Page_NowCount.Content = CurPage;
                lbl_Page_TotalCount.Content = RecordCount;
                lbl_Page_PageCount.Content = (int)Math.Ceiling(RecordCount / Convert.ToDecimal(PageSize));
            }
            catch (Exception ex)
            {
                SnackbarSeven.MessageQueue?.Enqueue(
                    ex.Message.ToString(),
                    null, null, null, false, true,
                    TimeSpan.FromSeconds(2));
            }
        }

        public void InitDataGrid()
        {
            CurPage = 1;
            PageSize = 20;
            RecordCount = 0;
            List();
        }

        public void ShowSnackbarSeven(string msg, double seconds)
        {
            SnackbarSeven.MessageQueue?.Enqueue(
                msg,
                null,
                null,
                null,
                false,
                true,
                TimeSpan.FromSeconds(seconds));
        }

        #region 分页按钮
        private void Btn_Page_FirstClick(object sender, RoutedEventArgs e)
        {
            CurPage = 1;
            RecordCount = 0;
            List();
        }

        private void Btn_Page_PrevClick(object sender, RoutedEventArgs e)
        {
            if (CurPage <= 1) return;
            CurPage--;
            RecordCount = 0;
            List();
        }

        private void Btn_Page_NextClick(object sender, RoutedEventArgs e)
        {
            if (CurPage > (int)Math.Ceiling(RecordCount / PageSize * 1.0)) return;
            CurPage++;
            RecordCount = 0;
            List();
        }

        private void Btn_Page_LastClick(object sender, RoutedEventArgs e)
        {
            CurPage = (int)Math.Ceiling(RecordCount / Convert.ToDecimal(PageSize));
            RecordCount = 0;
            List();
        }

        private void Btn_Page_GoClick(object sender, RoutedEventArgs e)
        {
            var goCurPage = 0;
            int.TryParse(txt_Page_To.Text, out goCurPage);
            if (goCurPage < 1)
                CurPage = 1;
            else if (goCurPage > RecordCount / PageSize)
                CurPage = (int)Math.Ceiling(RecordCount / Convert.ToDecimal(PageSize));
            else
                CurPage = goCurPage;
            RecordCount = 0;
            List();
        }
        #endregion
    }
}
