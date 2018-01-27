/// <prop name="viewer">The object with callable methods for viewing items.</prop>
/// <prop name="updater">The object with callable methods for updating data.</prop>
/// <prop name="beginIndex">Only for visual purposes, if needed.</prop>
/// <prop name="endIndex">Only for visual purposes, if needed.</prop>
/// <prop name="data">Used to render the items. The entire data array is rendered.</prop>
class MDACSDataView extends React.Component {
    constructor(props) {
        super(props)
    }

    shouldComponentUpdate(nextProps, nextState) {
        let nextData = nextProps.data;  
        let curData = this.props.data;

        if (curData === null && nextData === null) {
            return false;
        }

        if (curData === null || nextData === null) {
            return true;
        }

        if (curData.length !== nextData.length) {
            return true;
        }

        for (let x = 0; x < curData.length; ++x) {
            if (curData[x] !== nextData[x]) {
                return true;
            }
        }

        return false;
    }

    render() {
        let props = this.props;
        let data = props.data;
        let beginIndex = props.beginIndex;
        let endIndex = props.endIndex;

        if (data === null) {
            return <div>No data.</div>;
        }

        let tmp = [];

        for (let x = 0; x < data.length; ++x) {
            let item = data[x];

            if (item !== undefined && item !== null) {
                tmp.push(<MDACSDataItem 
                            viewer={props.viewer} 
                            updater={props.updater} 
                            index={x + beginIndex} 
                            key={item.security_id} 
                            item={item}/>);
            }
        }

        let footer = null;

        if (tmp.length === 0) {
            footer = <div>No items from {beginIndex} to {endIndex - 1}.</div>;
        }

        return  <div style={{backgroundColor: 'white'}}>
                <Table striped bordered>
                    <thead>
                        <tr>
                            <td>Index</td>
                            <td>State</td>
                            <td>Date</td>
                            <td>Time</td>
                            <td>User</td>
                            <td>Device</td>
                            <td>View</td>
                            <td style={{width: '100%'}}>Note</td>
                        </tr>
                    </thead>
                    <tbody>
                        {tmp}
                    </tbody>
                </Table>
                {footer}
                </div>;
    }
}